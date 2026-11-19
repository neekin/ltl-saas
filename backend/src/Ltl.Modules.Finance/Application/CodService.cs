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

public record RecordCodCollectionRequest(Guid WaybillId, decimal Amount, string? Note, Guid? IdempotencyKey = null);

public record CreateCodSettlementRequest(Guid WaybillId, decimal Amount);

/// <summary>COD 独立资金服务（COD-001）。与运费财务完全分离（AGENTS §6.5）。</summary>
public interface ICodService
{
    Task<CodOrderDto> EnsureCodOrderAsync(Guid waybillId);

    /// <summary>带权限校验的 COD 订单懒初始化（UI 首次登记收款前入口）。</summary>
    Task<CodOrderDto> EnsureCodOrderForOperatorAsync(Guid operatorUserId, Guid waybillId);

    /// <summary>按运单列出 COD 结算单（审批/付款界面数据源；新→旧）。</summary>
    Task<IReadOnlyList<CodSettlementDto>> ListSettlementsByWaybillAsync(Guid operatorUserId, Guid waybillId);
    Task<CodOrderDto?> GetCodOrderByWaybillAsync(Guid operatorUserId, Guid waybillId);
    Task<CodCollectionDto> RecordCollectionAsync(Guid operatorUserId, RecordCodCollectionRequest request);
    Task<CodSettlementDto> CreateSettlementAsync(Guid operatorUserId, CreateCodSettlementRequest request);
    Task<CodSettlementDto> ApproveSettlementAsync(Guid operatorUserId, Guid settlementId);
    Task<CodSettlementDto> PaySettlementAsync(Guid operatorUserId, Guid settlementId);

    /// <summary>COD 收款退款/冲正（追加式：保留原交易 + 反向记录）。</summary>
    Task<CodRefundRecordDto> RefundCollectionAsync(Guid operatorUserId, Guid collectionId, decimal amount, string reason);
}

public sealed record CodRefundRecordDto(
    Guid Id, Guid CollectionId, decimal Amount, string Reason, Guid? OperatorUserId, DateTime OccurredAt);

public sealed class CodService(
    FinanceDbContext db,
    IAuthorizationService authorization,
    IAuditLogService audit) : ICodService
{
    /// <summary>带权限校验的 COD 订单懒初始化。</summary>
    public async Task<CodOrderDto> EnsureCodOrderForOperatorAsync(Guid operatorUserId, Guid waybillId)
    {
        var tenantId = await RequirePermissionAsync(operatorUserId, "cod.manage");
        await RequireWaybillBranchAccessAsync(tenantId, operatorUserId, waybillId);
        return await EnsureCodOrderAsync(waybillId);
    }

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

    public async Task<CodOrderDto?> GetCodOrderByWaybillAsync(Guid operatorUserId, Guid waybillId)
    {
        var tenantId = await RequirePermissionAsync(operatorUserId, "cod.read");
        await RequireWaybillBranchAccessAsync(tenantId, operatorUserId, waybillId);
        var codOrder = await db.CodOrders
            .AsNoTracking()
            .SingleOrDefaultAsync(o => o.WaybillId == waybillId && o.TenantId == tenantId);
        return codOrder is null ? null : ToDto(codOrder);
    }

    /// <summary>按运单列出结算单（新→旧）。校验 cod.read 与运单网点范围。</summary>
    public async Task<IReadOnlyList<CodSettlementDto>> ListSettlementsByWaybillAsync(Guid operatorUserId, Guid waybillId)
    {
        var tenantId = await RequirePermissionAsync(operatorUserId, "cod.read");
        await RequireWaybillBranchAccessAsync(tenantId, operatorUserId, waybillId);
        var codOrder = await db.CodOrders
            .AsNoTracking()
            .SingleOrDefaultAsync(o => o.WaybillId == waybillId && o.TenantId == tenantId);
        if (codOrder is null)
        {
            return [];
        }

        var settlements = await db.CodSettlements
            .AsNoTracking()
            .Where(x => x.CodOrderId == codOrder.Id && x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
        return settlements.Select(ToDto).ToList();
    }

    public async Task<CodCollectionDto> RecordCollectionAsync(
        Guid operatorUserId, RecordCodCollectionRequest request)
    {
        var tenantId = await RequirePermissionAsync(operatorUserId, "cod.manage");
        await RequireWaybillBranchAccessAsync(tenantId, operatorUserId, request.WaybillId);

        // 幂等（AGENTS §8）：同租户同幂等键已存在 → 返回原收款记录
        if (request.IdempotencyKey is { } collectionKey)
        {
            var existingCollection = await db.CodCollections
                .AsNoTracking()
                .SingleOrDefaultAsync(c => c.IdempotencyKey == collectionKey);
            if (existingCollection is not null)
            {
                return new CodCollectionDto(
                    existingCollection.Id, existingCollection.CodOrderId, existingCollection.Amount,
                    existingCollection.OperatorUserId, existingCollection.Note, existingCollection.OccurredAt);
            }
        }

        var codOrder = await db.CodOrders
            .SingleOrDefaultAsync(o => o.WaybillId == request.WaybillId)
            ?? throw new EntityNotFoundException("COD 订单", request.WaybillId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        var collection = CodCollection.Create(
            tenantId, codOrder.Id, request.Amount,
            operatorUserId, request.Note, DateTime.UtcNow, request.IdempotencyKey);
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
        var tenantId = await RequirePermissionAsync(operatorUserId, "cod.manage");
        await RequireWaybillBranchAccessAsync(tenantId, operatorUserId, request.WaybillId);

        var codOrder = await db.CodOrders
            .SingleOrDefaultAsync(o => o.WaybillId == request.WaybillId && o.TenantId == tenantId)
            ?? throw new EntityNotFoundException("COD 订单", request.WaybillId);

        var available = codOrder.AvailableForSettlement;
        if (request.Amount > available)
        {
            throw new ConflictException(
                $"结算金额（{request.Amount}）超出可结算余额（{available}）。");
        }

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var settlement = CodSettlement.Create(
            tenantId, codOrder.Id, request.Amount,
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
        var tenantId = await RequirePermissionAsync(operatorUserId, "cod.manage");
        var settlement = await db.CodSettlements.SingleOrDefaultAsync(s => s.Id == settlementId && s.TenantId == tenantId)
            ?? throw new EntityNotFoundException("结算单", settlementId);
        var codOrder = await db.CodOrders.SingleOrDefaultAsync(o => o.Id == settlement.CodOrderId && o.TenantId == tenantId)
            ?? throw new EntityNotFoundException("COD 订单", settlement.CodOrderId);
        await RequireWaybillBranchAccessAsync(tenantId, operatorUserId, codOrder.WaybillId);

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
        var tenantId = await RequirePermissionAsync(operatorUserId, "cod.manage");
        var settlement = await db.CodSettlements.SingleOrDefaultAsync(s => s.Id == settlementId && s.TenantId == tenantId)
            ?? throw new EntityNotFoundException("结算单", settlementId);
        var codOrder = await db.CodOrders.SingleOrDefaultAsync(o => o.Id == settlement.CodOrderId && o.TenantId == tenantId)
            ?? throw new EntityNotFoundException("COD 订单", settlement.CodOrderId);
        await RequireWaybillBranchAccessAsync(tenantId, operatorUserId, codOrder.WaybillId);

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

    public async Task<CodRefundRecordDto> RefundCollectionAsync(
        Guid operatorUserId, Guid collectionId, decimal amount, string reason)
    {
        var tenantId = await RequirePermissionAsync(operatorUserId, "cod.manage");

        var collection = await db.CodCollections.SingleOrDefaultAsync(c => c.Id == collectionId && c.TenantId == tenantId)
            ?? throw new EntityNotFoundException("COD 收款记录", collectionId);
        var codOrder = await db.CodOrders.SingleOrDefaultAsync(o => o.Id == collection.CodOrderId && o.TenantId == tenantId)
            ?? throw new EntityNotFoundException("COD 订单", collection.CodOrderId);
        await RequireWaybillBranchAccessAsync(tenantId, operatorUserId, codOrder.WaybillId);

        // 已退金额 = Σ(refunds for this collection)
        var alreadyRefunded = await db.CodRefunds
            .Where(r => r.CollectionId == collectionId)
            .SumAsync(r => (decimal?)r.Amount) ?? 0;
        if (amount + alreadyRefunded > collection.Amount)
        {
            throw new ConflictException(
                $"COD 退款金额（{amount}）超出可退余额（{collection.Amount - alreadyRefunded}）。");
        }

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var refund = CodRefundRecord.Create(
            tenantId, collectionId, amount, reason,
            operatorUserId, DateTime.UtcNow);
        db.CodRefunds.Add(refund);

        // COD 订单 collected_amount 回退（领域方法）
        codOrder.ReverseCollection(amount);
        await db.SaveChangesAsync();

        await audit.RecordAsync(
            "cod.refund", "cod_refund_record", refund.Id, operatorUserId,
            new { collectionId = collectionId, amount = amount, reason = reason });
        scope.Complete();

        return new CodRefundRecordDto(
            refund.Id, refund.CollectionId, refund.Amount, refund.Reason,
            refund.OperatorUserId, refund.OccurredAt);
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
            throw new PermissionDeniedException("COD 数据范围（运单始发/目的网点）");
        }
    }

    private static CodOrderDto ToDto(CodOrder o) => new(
        o.Id, o.WaybillId, o.Amount, o.CollectedAmount, o.SettledAmount, o.PaidAmount, o.Status.ToString());

    private static CodSettlementDto ToDto(CodSettlement s) => new(
        s.Id, s.CodOrderId, s.Amount, s.Status.ToString(), s.ApprovedBy, s.ApprovedAt, s.PaidBy, s.PaidAt);

    private sealed record CodAmountRow(decimal? CodAmount);

    private sealed record WaybillBranchesRow(Guid OriginBranchId, Guid DestBranchId);
}
