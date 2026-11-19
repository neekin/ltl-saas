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

/// <summary>网点收款汇总行（FIN-003）：累计 + 当月两种口径。</summary>
public sealed record BranchPaymentSummary(
    Guid BranchId, string BranchCode, string BranchName,
    decimal TotalCollected, int PaymentCount,
    decimal MonthCollected, int MonthPaymentCount);

public record RecordPaymentRequest(
    Guid WaybillId, decimal Amount, string Method, string? Note, Guid? IdempotencyKey = null);

public record RefundPaymentRequest(Guid PaymentId, decimal Amount, string Reason);

/// <summary>运费财务服务（FIN-001）。签收不自动等于结清（AGENTS §6.1）；追加式流水（AGENTS §6.5）。</summary>
public interface IFinanceService
{
    /// <summary>确保应收存在（运单确认后首次收款时懒创建，金额 = 运单 TotalAmount）。</summary>
    Task<ReceivableDto> EnsureReceivableAsync(Guid waybillId);

    /// <summary>带权限校验的应收懒初始化（FIN-001 首笔收款前 UI 入口）。</summary>
    Task<ReceivableDto> EnsureReceivableForOperatorAsync(Guid operatorUserId, Guid waybillId);

    /// <summary>信用额度检查（月结客户；outstanding > credit_limit → false）。customerId 无额度 → true。</summary>
    Task<bool> IsWithinCreditLimitAsync(Guid customerId);

    Task<ReceivableDto?> GetReceivableAsync(Guid operatorUserId, Guid waybillId);
    Task<PaymentRecordDto> RecordPaymentAsync(Guid operatorUserId, RecordPaymentRequest request);
    Task<RefundRecordDto> RefundAsync(Guid operatorUserId, RefundPaymentRequest request);
    Task<IReadOnlyList<PaymentRecordDto>> ListPaymentsAsync(Guid operatorUserId, Guid waybillId);

    /// <summary>月结账单：按客户按期生成（汇总未收应收）。</summary>
    Task<CustomerStatement> GenerateStatementAsync(Guid operatorUserId, Guid customerId, string period);

    /// <summary>确认月结账单（Draft → Confirmed；明细冻结）。</summary>
    Task ConfirmStatementAsync(Guid operatorUserId, Guid statementId);

    /// <summary>关闭月结账单（Confirmed → Closed；终态）。</summary>
    Task CloseStatementAsync(Guid operatorUserId, Guid statementId);

    /// <summary>网点收款汇总（FIN-003）：按运单始发网点聚合收款笔数与金额；month 为 yyyyMM 可选。</summary>
    Task<IReadOnlyList<BranchPaymentSummary>> GetBranchPaymentSummaryAsync(Guid operatorUserId, string? month);
}

public sealed class FinanceService(
    FinanceDbContext db,
    IAuthorizationService authorization,
    IAuditLogService audit) : IFinanceService
{
    /// <summary>带权限校验的应收懒初始化（FIN-001 首笔收款前 UI 入口）。</summary>
    public async Task<ReceivableDto> EnsureReceivableForOperatorAsync(Guid operatorUserId, Guid waybillId)
    {
        var tenantId = await RequirePermissionAsync(operatorUserId, FinancePermissionCodes.FinanceManage);
        await RequireWaybillBranchAccessAsync(tenantId, operatorUserId, waybillId);
        return await EnsureReceivableAsync(waybillId);
    }

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

        var tenantId = db.CurrentTenantId.Value;
        var outstanding = (await db.Database
                .SqlQuery<OutstandingRow>(
                    $"""
                    SELECT COALESCE(SUM(r.amount - r.settled_amount), 0) AS "Outstanding"
                    FROM receivables r
                    INNER JOIN waybills w ON w.id = r.waybill_id AND w.tenant_id = r.tenant_id
                    WHERE r.tenant_id = {tenantId} AND w.customer_id = {customerId} AND r.status != {(int)ReceivableStatus.Settled}
                    """)
                .ToListAsync())
            .SingleOrDefault()?.Outstanding ?? 0m;
        return outstanding <= limit;
    }

    public async Task<ReceivableDto?> GetReceivableAsync(Guid operatorUserId, Guid waybillId)
    {
        var tenantId = await RequirePermissionAsync(operatorUserId, FinancePermissionCodes.FinanceRead);
        await RequireWaybillBranchAccessAsync(tenantId, operatorUserId, waybillId);
        var receivable = await db.Receivables
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.WaybillId == waybillId && r.TenantId == tenantId);
        return receivable is null ? null : ToDto(receivable);
    }

    public async Task<PaymentRecordDto> RecordPaymentAsync(Guid operatorUserId, RecordPaymentRequest request)
    {
        var tenantId = await RequirePermissionAsync(operatorUserId, FinancePermissionCodes.FinanceManage);
        await RequireWaybillBranchAccessAsync(tenantId, operatorUserId, request.WaybillId);

        // 幂等（AGENTS §8）：同租户同幂等键已存在 → 返回原收款记录，不再入账
        if (request.IdempotencyKey is { } paymentKey)
        {
            var existingPayment = await db.Payments
                .AsNoTracking()
                .SingleOrDefaultAsync(p => p.IdempotencyKey == paymentKey);
            if (existingPayment is not null)
            {
                return new PaymentRecordDto(
                    existingPayment.Id, existingPayment.WaybillId, existingPayment.Amount,
                    existingPayment.Method.ToString(), existingPayment.Note,
                    existingPayment.OperatorUserId, existingPayment.OccurredAt);
            }
        }

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

            receivable = Receivable.Create(tenantId, request.WaybillId, totalAmount, DateTime.UtcNow);
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
            tenantId, request.WaybillId, request.Amount, method,
            operatorUserId, request.Note, DateTime.UtcNow, operatorUserId, request.IdempotencyKey);
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var allocation = PaymentAllocation.Create(
            tenantId, payment.Id, receivable.Id, request.Amount, DateTime.UtcNow);
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
        var tenantId = await RequirePermissionAsync(operatorUserId, FinancePermissionCodes.FinanceManage);

        var payment = await db.Payments.SingleOrDefaultAsync(p => p.Id == request.PaymentId && p.TenantId == tenantId)
            ?? throw new EntityNotFoundException("收款记录", request.PaymentId);
        await RequireWaybillBranchAccessAsync(tenantId, operatorUserId, payment.WaybillId);

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
            tenantId, request.PaymentId, request.Amount, request.Reason,
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

    public async Task<IReadOnlyList<PaymentRecordDto>> ListPaymentsAsync(Guid operatorUserId, Guid waybillId)
    {
        var tenantId = await RequirePermissionAsync(operatorUserId, FinancePermissionCodes.FinanceRead);
        await RequireWaybillBranchAccessAsync(tenantId, operatorUserId, waybillId);

        return (await db.Payments
                .AsNoTracking()
                .Where(p => p.WaybillId == waybillId && p.TenantId == tenantId)
                .OrderBy(p => p.OccurredAt)
                .ToListAsync())
            .Select(p => new PaymentRecordDto(
                p.Id, p.WaybillId, p.Amount, p.Method.ToString(), p.Note, p.OperatorUserId, p.OccurredAt))
            .ToList();
    }

    public async Task<CustomerStatement> GenerateStatementAsync(Guid operatorUserId, Guid customerId, string period)
    {
        var tenantId = await RequirePermissionAsync(operatorUserId, FinancePermissionCodes.FinanceManage);

        var existing = await db.Statements
            .SingleOrDefaultAsync(s => s.CustomerId == customerId && s.Period == period);
        if (existing is not null)
            throw new ConflictException($"客户 {customerId} 在 {period} 期间已存在账单。");

        // 期间筛选：应收创建时间在指定月份内（期间筛选，FIN-002 设计要求）
        var periodStart = new DateTime(
            int.Parse(period[..4]), int.Parse(period[4..]), 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1);

        // 按客户筛选未收应收（原生 SQL 读取该客户在本租户下的运单 ID，不跨客户混算）
        var customerWaybillIds = (await db.Database
                .SqlQuery<WaybillIdRow>(
                    $"""SELECT id FROM waybills WHERE tenant_id = {tenantId} AND customer_id = {customerId}""")
                .ToListAsync())
            .Select(x => x.Id)
            .ToList();
        var receivables = customerWaybillIds.Count == 0
            ? []
            : await db.Receivables
                .Where(r => r.Status != ReceivableStatus.Settled
                    && customerWaybillIds.Contains(r.WaybillId)
                    && r.CreatedAt >= periodStart && r.CreatedAt < periodEnd)
                .ToListAsync();
        if (receivables.Count == 0)
            throw new InvalidOperationException($"该客户在 {period} 期间无未收应收，无需生成账单。");

        var totalAmount = receivables.Sum(r => r.Amount - r.SettledAmount);

        var statement = CustomerStatement.Create(
            tenantId, customerId, period, totalAmount, DateTime.UtcNow, operatorUserId);
        db.Statements.Add(statement);
        await db.SaveChangesAsync();

        // 逐应收创建账单明细（快照金额）
        foreach (var receivable in receivables)
        {
            db.StatementItems.Add(CustomerStatementItem.Create(
                tenantId, statement.Id, receivable.Id, receivable.WaybillId,
                receivable.Amount, receivable.SettledAmount, DateTime.UtcNow));
        }
        await db.SaveChangesAsync();

        return statement;
    }

    public async Task ConfirmStatementAsync(Guid operatorUserId, Guid statementId)
    {
        var tenantId = await RequirePermissionAsync(operatorUserId, FinancePermissionCodes.FinanceManage);
        var statement = await db.Statements.SingleOrDefaultAsync(s => s.Id == statementId && s.TenantId == tenantId)
            ?? throw new EntityNotFoundException("月结账单", statementId);
        statement.Confirm(DateTime.UtcNow, operatorUserId);
        await db.SaveChangesAsync();
    }

    public async Task CloseStatementAsync(Guid operatorUserId, Guid statementId)
    {
        var tenantId = await RequirePermissionAsync(operatorUserId, FinancePermissionCodes.FinanceManage);
        var statement = await db.Statements.SingleOrDefaultAsync(s => s.Id == statementId && s.TenantId == tenantId)
            ?? throw new EntityNotFoundException("月结账单", statementId);
        statement.Close(DateTime.UtcNow, operatorUserId);
        await db.SaveChangesAsync();
    }

    /// <summary>网点收款汇总：原生 SQL 跨表聚合（AGENTS §5.1），按用户授权网点范围过滤（AGENTS §5.2）。</summary>
    public async Task<IReadOnlyList<BranchPaymentSummary>> GetBranchPaymentSummaryAsync(
        Guid operatorUserId, string? month)
    {
        var tenantId = await RequirePermissionAsync(operatorUserId, FinancePermissionCodes.FinanceRead);
        var allowedBranchIds = (await authorization.GetAuthorizedBranchIdsAsync(operatorUserId)).ToHashSet();
        if (allowedBranchIds.Count == 0)
        {
            return [];
        }

        // month 为 yyyyMM 时按指定月份筛选当月收款；未传时默认当前月（与 REPORT-001 口径一致）
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);
        if (month is { Length: 6 } && int.TryParse(month[..4], out var y)
            && int.TryParse(month[4..], out var m) && y >= 2000 && y <= 9999 && m >= 1 && m <= 12)
        {
            start = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc);
            end = start.AddMonths(1);
        }

        var rows = await db.Database
            .SqlQuery<BranchPaymentRow>($"""
                SELECT b.id AS "branch_id", b.code AS "branch_code", b.name AS "branch_name",
                       COALESCE(SUM(p.amount), 0) AS "total_collected",
                       COUNT(p.id)::integer AS "payment_count",
                       COALESCE(SUM(CASE WHEN p.occurred_at >= {start} AND p.occurred_at < {end}
                           THEN p.amount ELSE 0 END), 0) AS "month_collected",
                       COUNT(CASE WHEN p.occurred_at >= {start} AND p.occurred_at < {end}
                           THEN 1 END)::integer AS "month_payment_count"
                FROM branches b
                LEFT JOIN waybills w ON w.tenant_id = b.tenant_id AND w.origin_branch_id = b.id
                LEFT JOIN payment_records p ON p.tenant_id = w.tenant_id AND p.waybill_id = w.id
                WHERE b.tenant_id = {tenantId}
                GROUP BY b.id, b.code, b.name
                ORDER BY "total_collected" DESC, b.code
                """)
            .ToListAsync();

        return rows
            .Where(r => allowedBranchIds.Contains(r.BranchId))
            .Select(r => new BranchPaymentSummary(
                r.BranchId, r.BranchCode, r.BranchName,
                r.TotalCollected, r.PaymentCount, r.MonthCollected, r.MonthPaymentCount))
            .ToList();
    }

    private async Task<Guid> RequirePermissionAsync(Guid operatorUserId, string permissionCode)
    {
        if (db.CurrentTenantId is not { } tenantId)
            throw new TenantContextMissingException();
        if (!await authorization.HasPermissionAsync(operatorUserId, permissionCode))
            throw new PermissionDeniedException(permissionCode);
        return tenantId;
    }

    private async Task RequireWaybillBranchAccessAsync(Guid tenantId, Guid operatorUserId, Guid waybillId)
    {
        var branches = (await db.Database
                .SqlQuery<WaybillBranchesRow>(
                    $"""
                    SELECT origin_branch_id, dest_branch_id
                    FROM waybills
                    WHERE tenant_id = {tenantId} AND id = {waybillId}
                    """)
                .ToListAsync())
            .SingleOrDefault()
            ?? throw new EntityNotFoundException("运单", waybillId);

        if (!await authorization.CanAccessBranchAsync(operatorUserId, branches.OriginBranchId)
            && !await authorization.CanAccessBranchAsync(operatorUserId, branches.DestBranchId))
        {
            throw new PermissionDeniedException("财务数据范围（运单始发/目的网点）");
        }
    }

    private sealed record BranchPaymentRow(
        Guid BranchId, string BranchCode, string BranchName,
        decimal TotalCollected, int PaymentCount,
        decimal MonthCollected, int MonthPaymentCount);

    private sealed record TotalAmountRow(decimal? TotalAmount);

    private sealed record CreditLimitRow(decimal? CreditLimit);

    private sealed record OutstandingRow(decimal Outstanding);

    private sealed record WaybillIdRow(Guid Id);

    private sealed record WaybillBranchesRow(Guid OriginBranchId, Guid DestBranchId);

    private static ReceivableDto ToDto(Receivable r) => new(
        r.Id, r.WaybillId, r.Amount, r.SettledAmount, r.Outstanding, r.Status.ToString());
}
