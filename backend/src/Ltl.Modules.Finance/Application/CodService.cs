using System.Transactions;
using Ltl.Modules.Finance.Domain;
using Ltl.Modules.Finance.Infrastructure;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Platform.Application;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Finance.Application;

public sealed record CodOrderDto(
    Guid Id, Guid WaybillId, decimal Amount, decimal CollectedAmount,
    decimal SettledAmount, decimal PaidAmount, string Status);

public sealed record CodCollectionDto(
    Guid Id, Guid CodOrderId, decimal Amount, Guid? OperatorUserId, string? Note, DateTime OccurredAt);

public sealed record CodSettlementDto(
    Guid Id, Guid CodOrderId, decimal Amount, string Status,
    Guid? ApprovedBy, DateTime? ApprovedAt, Guid? PaidBy, DateTime? PaidAt);

public record RecordCodCollectionRequest(Guid WaybillId, decimal Amount, string? Note);

public record CreateCodSettlementRequest(Guid WaybillId, decimal Amount);

/// <summary>COD 独立资金服务（COD-001）。与运费财务完全分离（AGENTS §6.5）。</summary>
public interface ICodService
{
    Task<CodOrderDto> EnsureCodOrderAsync(Guid waybillId);
    Task<CodOrderDto?> GetCodOrderByWaybillAsync(Guid waybillId);
    Task<CodCollectionDto> RecordCollectionAsync(Guid operatorUserId, RecordCodCollectionRequest request);
    Task<CodSettlementDto> CreateSettlementAsync(Guid operatorUserId, CreateCodSettlementRequest request);
    Task<CodSettlementDto> ApproveSettlementAsync(Guid operatorUserId, Guid settlementId);
    Task<CodSettlementDto> PaySettlementAsync(Guid operatorUserId, Guid settlementId);
}

public sealed class CodService(
    FinanceDbContext db,
    IAuthorizationService authorization,
    IAuditLogService audit) : ICodService
{
    public async Task<CodOrderDto> EnsureCodOrderAsync(Guid waybillId)
    {
        var existing = await db.CodOrders
            .SingleOrDefaultAsync(o => o.WaybillId == waybillId);
        if (existing is not null)
            return ToDto(existing);

        var tenantId = db.CurrentTenantId
            ?? throw new TenantContextMissingException();

        var codAmount = (await db.Database
                .SqlQuery<CodAmountRow>(
                    $"SELECT cod_amount AS \"CodAmount\" FROM waybills WHERE tenant_id = {tenantId} AND id = {waybillId}")
                .ToListAsync())
            .SingleOrDefault()?.CodAmount;

        if (codAmount is not { } amount || amount <= 0)
        {
            throw new InvalidOperationException("运单无代收货款金额，无法创建 COD 订单。");
        }

        var codOrder = CodOrder.Create(tenantId, waybillId, amount, DateTime.UtcNow);
        db.CodOrders.Add(codOrder);
        await db.SaveChangesAsync();
        return ToDto(codOrder);
    }

    public async Task<CodOrderDto?> GetCodOrderByWaybillAsync(Guid waybillId)
    {
        var codOrder = await db.CodOrders
            .AsNoTracking()
            .SingleOrDefaultAsync(o => o.WaybillId == waybillId);
        return codOrder is null ? null : ToDto(codOrder);
    }

    public async Task<CodCollectionDto> RecordCollectionAsync(
        Guid operatorUserId, RecordCodCollectionRequest request)
    {
        if (db.CurrentTenantId is null)
            throw new TenantContextMissingException();

        var codOrder = await db.CodOrders
            .SingleOrDefaultAsync(o => o.WaybillId == request.WaybillId)
            ?? throw new EntityNotFoundException("COD 订单", request.WaybillId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        var collection = CodCollection.Create(
            db.CurrentTenantId.Value, codOrder.Id, request.Amount,
            operatorUserId, request.Note, DateTime.UtcNow);
        db.CodCollections.Add(collection);

        codOrder.ApplyCollection(request.Amount);
        await db.SaveChangesAsync();

        await audit.RecordAsync(
            "cod.collection", "cod_order", codOrder.Id, operatorUserId,
            new { waybillId = request.WaybillId, amount = request.Amount });
        scope.Complete();

        return new CodCollectionDto(
            collection.Id, collection.CodOrderId, collection.Amount,
            collection.OperatorUserId, collection.Note, collection.OccurredAt);
    }

    public async Task<CodSettlementDto> CreateSettlementAsync(
        Guid operatorUserId, CreateCodSettlementRequest request)
    {
        if (db.CurrentTenantId is null)
            throw new TenantContextMissingException();

        var codOrder = await db.CodOrders
            .SingleOrDefaultAsync(o => o.WaybillId == request.WaybillId)
            ?? throw new EntityNotFoundException("COD 订单", request.WaybillId);

        var available = codOrder.AvailableForSettlement;
        if (request.Amount > available)
        {
            throw new ConflictException(
                $"结算金额（{request.Amount}）超出可结算余额（{available}）。");
        }

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var settlement = CodSettlement.Create(
            db.CurrentTenantId.Value, codOrder.Id, request.Amount,
            operatorUserId, DateTime.UtcNow);
        db.CodSettlements.Add(settlement);
        await db.SaveChangesAsync();

        codOrder.ApplySettlement(request.Amount);
        await db.SaveChangesAsync();

        await audit.RecordAsync(
            "cod.settlement.create", "cod_settlement", settlement.Id, operatorUserId,
            new { waybillId = request.WaybillId, amount = request.Amount });
        scope.Complete();

        return ToDto(settlement);
    }

    public async Task<CodSettlementDto> ApproveSettlementAsync(Guid operatorUserId, Guid settlementId)
    {
        var settlement = await db.CodSettlements.FindAsync([settlementId])
            ?? throw new EntityNotFoundException("结算单", settlementId);
        if (db.CurrentTenantId is null)
            throw new TenantContextMissingException();

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        settlement.Approve(operatorUserId, DateTime.UtcNow);
        await db.SaveChangesAsync();
        await audit.RecordAsync(
            "cod.settlement.approve", "cod_settlement", settlement.Id, operatorUserId,
            new { codOrderId = settlement.CodOrderId, amount = settlement.Amount });
        scope.Complete();

        return ToDto(settlement);
    }

    public async Task<CodSettlementDto> PaySettlementAsync(Guid operatorUserId, Guid settlementId)
    {
        var settlement = await db.CodSettlements.FindAsync([settlementId])
            ?? throw new EntityNotFoundException("结算单", settlementId);
        if (db.CurrentTenantId is null)
            throw new TenantContextMissingException();

        var codOrder = await db.CodOrders.FindAsync([settlement.CodOrderId])
            ?? throw new EntityNotFoundException("COD 订单", settlement.CodOrderId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        settlement.MarkPaidOut(operatorUserId, DateTime.UtcNow);
        await db.SaveChangesAsync();

        codOrder.ApplyPayment(settlement.Amount);
        await db.SaveChangesAsync();

        await audit.RecordAsync(
            "cod.settlement.pay", "cod_settlement", settlement.Id, operatorUserId,
            new { codOrderId = settlement.CodOrderId, amount = settlement.Amount });
        scope.Complete();

        return ToDto(settlement);
    }

    private static CodOrderDto ToDto(CodOrder o) => new(
        o.Id, o.WaybillId, o.Amount, o.CollectedAmount, o.SettledAmount, o.PaidAmount, o.Status.ToString());

    private static CodSettlementDto ToDto(CodSettlement s) => new(
        s.Id, s.CodOrderId, s.Amount, s.Status.ToString(), s.ApprovedBy, s.ApprovedAt, s.PaidBy, s.PaidAt);

    private sealed record CodAmountRow(decimal? CodAmount);
}
