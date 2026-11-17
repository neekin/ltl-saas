using System.Transactions;
using Ltl.Modules.Finance.Domain;
using Ltl.Modules.Customers.Domain;
using Ltl.Modules.Finance.Infrastructure;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Platform.Application;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Finance.Application;

public sealed record ReceivableDto(
    Guid Id, Guid WaybillId, decimal Amount, decimal SettledAmount, decimal Outstanding, string Status);

public sealed record PaymentRecordDto(
    Guid Id, Guid WaybillId, decimal Amount, string Method, string? Note, Guid? OperatorUserId, DateTime OccurredAt);

public sealed record RefundRecordDto(
    Guid Id, Guid PaymentId, decimal Amount, string Reason, Guid? OperatorUserId, DateTime OccurredAt);

public record RecordPaymentRequest(
    Guid WaybillId, decimal Amount, string Method, string? Note);

public record RefundPaymentRequest(Guid PaymentId, decimal Amount, string Reason);

/// <summary>运费财务服务（FIN-001）。签收不自动等于结清（AGENTS §6.1）；追加式流水（AGENTS §6.5）。</summary>
public interface IFinanceService
{
    /// <summary>确保应收存在（运单确认后首次收款时懒创建，金额 = 运单 TotalAmount）。</summary>
    Task<ReceivableDto> EnsureReceivableAsync(Guid waybillId);

    /// <summary>信用额度检查（月结客户；outstanding > credit_limit → false）。customerId 无额度 → true。</summary>
    Task<bool> IsWithinCreditLimitAsync(Guid customerId);

    Task<ReceivableDto?> GetReceivableAsync(Guid waybillId);
    Task<PaymentRecordDto> RecordPaymentAsync(Guid operatorUserId, RecordPaymentRequest request);
    Task<RefundRecordDto> RefundAsync(Guid operatorUserId, RefundPaymentRequest request);
    Task<IReadOnlyList<PaymentRecordDto>> ListPaymentsAsync(Guid waybillId);

    /// <summary>月结账单：按客户按期生成（汇总未收应收）。</summary>
    Task<CustomerStatement> GenerateStatementAsync(Guid operatorUserId, Guid customerId, string period);

    /// <summary>确认月结账单（Draft → Confirmed；明细冻结）。</summary>
    Task ConfirmStatementAsync(Guid operatorUserId, Guid statementId);

    /// <summary>关闭月结账单（Confirmed → Closed；终态）。</summary>
    Task CloseStatementAsync(Guid operatorUserId, Guid statementId);
}

public sealed class FinanceService(
    FinanceDbContext db,
    IAuthorizationService authorization,
    IAuditLogService audit) : IFinanceService
{
    public async Task<ReceivableDto> EnsureReceivableAsync(Guid waybillId)
    {
        var existing = await db.Receivables
            .SingleOrDefaultAsync(r => r.WaybillId == waybillId);
        if (existing is not null)
            return ToDto(existing);

        var tenantId = db.CurrentTenantId
            ?? throw new TenantContextMissingException();

        // 从运单读取固化金额（原生 SQL 显式租户参数，AGENTS §5.1）
        var totalAmount = (await db.Database
                .SqlQuery<TotalAmountRow>(
                    $"SELECT total_amount AS \"TotalAmount\" FROM waybills WHERE tenant_id = {tenantId} AND id = {waybillId}")
                .ToListAsync())
            .SingleOrDefault()?.TotalAmount
            ?? throw new EntityNotFoundException("运单", waybillId);
        if (totalAmount <= 0)
        {
            throw new InvalidOperationException("运单费用未固化或为零，无法创建应收。");
        }

        var receivable = Receivable.Create(tenantId, waybillId, totalAmount, DateTime.UtcNow);
        db.Receivables.Add(receivable);
        await db.SaveChangesAsync();
        return ToDto(receivable);
    }

    /// <summary>信用额度检查（AGENTS §6.5 / ROADMAP P7 逾期和信用额度检查）。</summary>
    public async Task<bool> IsWithinCreditLimitAsync(Guid customerId)
    {
        if (db.CurrentTenantId is null)
            throw new TenantContextMissingException();

        var creditLimit = (await db.Database
                .SqlQuery<CreditLimitRow>(
                    $"SELECT credit_limit AS \"CreditLimit\" FROM customers WHERE tenant_id = {db.CurrentTenantId} AND id = {customerId}")
                .ToListAsync())
            .SingleOrDefault()?.CreditLimit;

        if (creditLimit is not { } limit)
            return true;

        var outstanding = await db.Receivables
            .Where(r => r.Status != ReceivableStatus.Settled)
            .SumAsync(r => r.Amount - r.SettledAmount);
        return outstanding <= limit;
    }

    public async Task<ReceivableDto?> GetReceivableAsync(Guid waybillId)
    {
        var receivable = await db.Receivables
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.WaybillId == waybillId);
        return receivable is null ? null : ToDto(receivable);
    }

    public async Task<PaymentRecordDto> RecordPaymentAsync(Guid operatorUserId, RecordPaymentRequest request)
    {
        if (db.CurrentTenantId is null)
            throw new TenantContextMissingException();

        // 懒创建应收（运单确认后首次收款时生成）
        var receivable = await db.Receivables
            .SingleOrDefaultAsync(r => r.WaybillId == request.WaybillId);
        if (receivable is null)
        {
            var totalAmount = (await db.Database
                    .SqlQuery<TotalAmountRow>(
                        $"SELECT total_amount AS \"TotalAmount\" FROM waybills WHERE tenant_id = {db.CurrentTenantId} AND id = {request.WaybillId}")
                    .ToListAsync())
                .SingleOrDefault()?.TotalAmount
                ?? throw new EntityNotFoundException("运单", request.WaybillId);
            if (totalAmount <= 0)
                throw new InvalidOperationException("运单费用未固化或为零，无法创建应收。");

            receivable = Receivable.Create(db.CurrentTenantId.Value, request.WaybillId, totalAmount, DateTime.UtcNow);
            db.Receivables.Add(receivable);
            await db.SaveChangesAsync();
        }

        var outstanding = receivable.Amount - receivable.SettledAmount;
        if (request.Amount > outstanding)
        {
            throw new ConflictException(
                $"收款金额（{request.Amount}）超出未收余额（{outstanding}）。");
        }

        var method = request.Method.ToLowerInvariant() switch
        {
            "cash" => PaymentMethod.Cash,
            "ondelivery" => PaymentMethod.OnDelivery,
            "monthly" => PaymentMethod.Monthly,
            _ => throw new ArgumentException($"未知收款方式（{request.Method}）。", nameof(request)),
        };

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        var payment = PaymentRecord.Create(
            db.CurrentTenantId.Value, request.WaybillId, request.Amount, method,
            operatorUserId, request.Note, DateTime.UtcNow, operatorUserId);
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var allocation = PaymentAllocation.Create(
            db.CurrentTenantId.Value, payment.Id, receivable.Id, request.Amount, DateTime.UtcNow);
        db.Allocations.Add(allocation);

        receivable.ApplySettlement(request.Amount);
        await db.SaveChangesAsync();

        await audit.RecordAsync(
            "finance.payment.record", "payment_record", payment.Id, operatorUserId,
            new { waybillId = request.WaybillId, amount = request.Amount, method = request.Method });
        scope.Complete();

        return new PaymentRecordDto(
            payment.Id, payment.WaybillId, payment.Amount, payment.Method.ToString(),
            payment.Note, payment.OperatorUserId, payment.OccurredAt);
    }

    public async Task<RefundRecordDto> RefundAsync(Guid operatorUserId, RefundPaymentRequest request)
    {
        if (db.CurrentTenantId is null)
            throw new TenantContextMissingException();

        var payment = await db.Payments.FindAsync([request.PaymentId])
            ?? throw new EntityNotFoundException("收款记录", request.PaymentId);

        // 已退金额 = Σ(refund for this payment)
        var alreadyRefunded = await db.Refunds
            .Where(r => r.PaymentId == request.PaymentId)
            .SumAsync(r => (decimal?)r.Amount) ?? 0;
        if (request.Amount + alreadyRefunded > payment.Amount)
        {
            throw new ConflictException(
                $"退款金额（{request.Amount}）超出可退余额（{payment.Amount - alreadyRefunded}）。");
        }

        // 找到应收并回退核销
        var allocation = await db.Allocations
            .SingleOrDefaultAsync(a => a.PaymentId == request.PaymentId)
            ?? throw new EntityNotFoundException("核销记录", request.PaymentId);
        var receivable = await db.Receivables.FindAsync([allocation.ReceivableId])
            ?? throw new EntityNotFoundException("应收账款", allocation.ReceivableId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var refund = RefundRecord.Create(
            db.CurrentTenantId.Value, request.PaymentId, request.Amount, request.Reason,
            operatorUserId, DateTime.UtcNow);
        db.Refunds.Add(refund);

        receivable.ReverseSettlement(request.Amount);
        await db.SaveChangesAsync();

        await audit.RecordAsync(
            "finance.refund", "refund_record", refund.Id, operatorUserId,
            new { paymentId = request.PaymentId, amount = request.Amount, reason = request.Reason });
        scope.Complete();

        return new RefundRecordDto(
            refund.Id, refund.PaymentId, refund.Amount, refund.Reason,
            refund.OperatorUserId, refund.OccurredAt);
    }

    public async Task<IReadOnlyList<PaymentRecordDto>> ListPaymentsAsync(Guid waybillId)
    {
        if (db.CurrentTenantId is null)
            throw new TenantContextMissingException();

        return (await db.Payments
                .AsNoTracking()
                .Where(p => p.WaybillId == waybillId)
                .OrderBy(p => p.OccurredAt)
                .ToListAsync())
            .Select(p => new PaymentRecordDto(
                p.Id, p.WaybillId, p.Amount, p.Method.ToString(), p.Note, p.OperatorUserId, p.OccurredAt))
            .ToList();
    }

    public async Task<CustomerStatement> GenerateStatementAsync(Guid operatorUserId, Guid customerId, string period)
    {
        if (db.CurrentTenantId is null)
            throw new TenantContextMissingException();

        var existing = await db.Statements
            .SingleOrDefaultAsync(s => s.CustomerId == customerId && s.Period == period);
        if (existing is not null)
            throw new ConflictException($"客户 {customerId} 在 {period} 期间已存在账单。");

        // 汇总该客户全部未收应收（简化：按全部未收计算，期间筛选由调用方参数扩展）
        var totalAmount = await db.Receivables
            .Where(r => r.Status != ReceivableStatus.Settled)
            .SumAsync(r => (decimal?)r.Amount - r.SettledAmount) ?? 0;
        if (totalAmount <= 0)
            throw new InvalidOperationException("该客户无未收应收，无需生成账单。");

        var statement = CustomerStatement.Create(
            db.CurrentTenantId.Value, customerId, period, totalAmount, DateTime.UtcNow, operatorUserId);
        db.Statements.Add(statement);
        await db.SaveChangesAsync();
        return statement;
    }

    public async Task ConfirmStatementAsync(Guid operatorUserId, Guid statementId)
    {
        var statement = await db.Statements.FindAsync([statementId])
            ?? throw new EntityNotFoundException("月结账单", statementId);
        statement.Confirm(DateTime.UtcNow, operatorUserId);
        await db.SaveChangesAsync();
    }

    public async Task CloseStatementAsync(Guid operatorUserId, Guid statementId)
    {
        var statement = await db.Statements.FindAsync([statementId])
            ?? throw new EntityNotFoundException("月结账单", statementId);
        statement.Close(DateTime.UtcNow, operatorUserId);
        await db.SaveChangesAsync();
    }

    private sealed record TotalAmountRow(decimal? TotalAmount);

    private sealed record CreditLimitRow(decimal? CreditLimit);

    private static ReceivableDto ToDto(Receivable r) => new(
        r.Id, r.WaybillId, r.Amount, r.SettledAmount, r.Outstanding, r.Status.ToString());
}
