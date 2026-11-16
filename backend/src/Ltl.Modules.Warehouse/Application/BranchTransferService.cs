using System.Transactions;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Platform.Application;
using Ltl.Modules.Platform.Domain;
using Ltl.Modules.Warehouse.Domain;
using Ltl.Modules.Warehouse.Infrastructure;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ltl.Modules.Warehouse.Application;

public sealed record BranchTransferItemDto(
    Guid Id, int Seq, string CargoName, int DeclaredQuantity,
    int? ReceivedQuantity, int? BrokenQuantity, string? Remark);

public sealed record BranchTransferDto(
    Guid Id, string TransferNo, Guid FromBranchId, Guid ToBranchId, string Status,
    IReadOnlyList<BranchTransferItemDto> Items, string? Note, DateTime CreatedAt,
    DateTime? SubmittedAt, DateTime? DispatchedAt, DateTime? ReceivedAt);

public sealed record CreateTransferRequest(
    Guid FromBranchId, Guid ToBranchId,
    IReadOnlyList<CreateTransferItemRequest> Items, string? Note);

public sealed record CreateTransferItemRequest(string CargoName, int Quantity, string? Remark);

public sealed record ReceiveTransferRequest(
    IReadOnlyList<ReceiveTransferItemRequest> Items);

public sealed record ReceiveTransferItemRequest(Guid ItemId, int ReceivedQuantity, int BrokenQuantity, string? Remark);

/// <summary>网点调拨服务（TRANSFER-001）：创建/提交/发出/验收/取消。调拨不产生对外销售收入。</summary>
public interface IBranchTransferService
{
    Task<BranchTransferDto> CreateAsync(Guid operatorUserId, CreateTransferRequest request);
    Task<BranchTransferDto> GetAsync(Guid operatorUserId, Guid transferId);
    Task<BranchTransferDto> SubmitAsync(Guid operatorUserId, Guid transferId);
    Task<BranchTransferDto> DispatchAsync(Guid operatorUserId, Guid transferId);
    Task<BranchTransferDto> ReceiveAsync(Guid operatorUserId, Guid transferId, ReceiveTransferRequest request);
    Task<BranchTransferDto> CancelAsync(Guid operatorUserId, Guid transferId);

    /// <summary>调拨单列表（分页，时间倒序；TRANSFER-001 前端列表页）。</summary>
    Task<PageResultTransfers> ListAsync(Guid operatorUserId, int page, int pageSize);
}

public sealed record PageResultTransfers(
    IReadOnlyList<BranchTransferDto> Items, int Total, int Page, int PageSize);

/// <summary>
/// 调拨实现（TRANSFER-001）。组合授权：调出侧动作校验调出网点范围、验收校验目的网点范围（AGENTS §5.2）；
/// 状态机经聚合守卫；单号 SEQ-001（DB + 调出网点码 + 期间 + 序号）；写操作 + 审计同环境事务。
/// </summary>
public sealed class BranchTransferService(
    WarehouseDbContext db,
    IBranchSequenceService branchSequenceService,
    IAuthorizationService authorization,
    IAuditLogService audit) : IBranchTransferService
{
    public async Task<BranchTransferDto> CreateAsync(Guid operatorUserId, CreateTransferRequest request)
    {
        var tenantId = await RequireTenantIdAsync(operatorUserId, WarehousePermissionCodes.TransferManage);

        // 调出网点范围（AGENTS §5.2）
        if (!await authorization.AuthorizeAsync(
                operatorUserId, request.FromBranchId, WarehousePermissionCodes.TransferManage))
        {
            throw new PermissionDeniedException(WarehousePermissionCodes.TransferManage);
        }

        // 调入网点存在且启用（原生 SQL 显式租户参数）
        var toBranchActive = (await db.Database
                .SqlQuery<StatusRow>(
                    $"""SELECT status AS "Status" FROM branches WHERE tenant_id = {tenantId} AND id = {request.ToBranchId}""")
                .ToListAsync())
            .SingleOrDefault() is { Status: 0 };
        if (!toBranchActive)
        {
            throw new EntityNotFoundException("调入网点", request.ToBranchId);
        }

        var fromCode = await RequireBranchCodeAsync(tenantId, request.FromBranchId);
        var transferNo = await branchSequenceService.NextNumberAsync(
            request.FromBranchId, BusinessDocumentType.BranchTransfer,
            DateOnly.FromDateTime(DateTime.UtcNow), fromCode);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var transfer = BranchTransfer.Create(
            tenantId, transferNo, request.FromBranchId, request.ToBranchId,
            request.Items.Select(i => (i.CargoName, i.Quantity, i.Remark)).ToList(),
            DateTime.UtcNow, operatorUserId, request.Note);
        db.Transfers.Add(transfer);
        await db.SaveChangesAsync();

        await audit.RecordAsync(
            "warehouse.transfer.create", "branch_transfer", transfer.Id, operatorUserId,
            new { transferNo = transfer.TransferNo, itemCount = transfer.Items.Count });
        var detail = await MapDetailAsync(transfer);
        scope.Complete();

        return detail;
    }

    public async Task<BranchTransferDto> GetAsync(Guid operatorUserId, Guid transferId)
    {
        await RequireTenantIdAsync(operatorUserId, WarehousePermissionCodes.TransferRead);
        var transfer = await RequireTransferAsync(transferId);
        return await MapDetailAsync(transfer);
    }

    public async Task<BranchTransferDto> SubmitAsync(Guid operatorUserId, Guid transferId)
    {
        var transfer = await RequireTransferAsync(transferId);
        await RequireTenantIdAsync(operatorUserId, WarehousePermissionCodes.TransferManage);
        await RequireFromBranchScopeAsync(operatorUserId, transfer);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        transfer.Submit(DateTime.UtcNow, operatorUserId);
        await db.SaveChangesAsync();
        await RecordAuditAsync("warehouse.transfer.submit", transfer, operatorUserId);
        var detailsubmit = await MapDetailAsync(transfer);
        scope.Complete();

        return detailsubmit;
    }

    public async Task<BranchTransferDto> DispatchAsync(Guid operatorUserId, Guid transferId)
    {
        var transfer = await RequireTransferAsync(transferId);
        await RequireTenantIdAsync(operatorUserId, WarehousePermissionCodes.TransferManage);
        await RequireFromBranchScopeAsync(operatorUserId, transfer);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        transfer.Dispatch(DateTime.UtcNow, operatorUserId);
        await db.SaveChangesAsync();
        await RecordAuditAsync("warehouse.transfer.dispatch", transfer, operatorUserId);
        var detaildispatch = await MapDetailAsync(transfer);
        scope.Complete();

        return detaildispatch;
    }

    public async Task<BranchTransferDto> ReceiveAsync(
        Guid operatorUserId, Guid transferId, ReceiveTransferRequest request)
    {
        var transfer = await RequireTransferAsync(transferId);
        await RequireTenantIdAsync(operatorUserId, WarehousePermissionCodes.TransferManage);

        // 验收在目的网点执行：目的网点范围（AGENTS §5.2）
        if (!await authorization.AuthorizeAsync(
                operatorUserId, transfer.ToBranchId, WarehousePermissionCodes.TransferManage))
        {
            throw new PermissionDeniedException("warehouse.transfer.receive（目的网点范围）");
        }

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        transfer.Receive(
            request.Items.Select(i => (i.ItemId, i.ReceivedQuantity, i.BrokenQuantity, i.Remark)).ToList(),
            DateTime.UtcNow, operatorUserId);
        await db.SaveChangesAsync();

        await audit.RecordAsync(
            "warehouse.transfer.receive", "branch_transfer", transfer.Id, operatorUserId,
            new
            {
                transferNo = transfer.TransferNo,
                items = transfer.Items.Select(i => new
                {
                    seq = i.Seq,
                    declared = i.DeclaredQuantity,
                    received = i.ReceivedQuantity,
                    broken = i.BrokenQuantity,
                }),
            });
        var detail = await MapDetailAsync(transfer);
        scope.Complete();

        return detail;
    }

    public async Task<BranchTransferDto> CancelAsync(Guid operatorUserId, Guid transferId)
    {
        var transfer = await RequireTransferAsync(transferId);
        await RequireTenantIdAsync(operatorUserId, WarehousePermissionCodes.TransferManage);
        await RequireFromBranchScopeAsync(operatorUserId, transfer);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        transfer.Cancel(DateTime.UtcNow, operatorUserId);
        await db.SaveChangesAsync();
        await RecordAuditAsync("warehouse.transfer.cancel", transfer, operatorUserId);
        var detailcancel = await MapDetailAsync(transfer);
        scope.Complete();

        return detailcancel;
    }

    public async Task<PageResultTransfers> ListAsync(Guid operatorUserId, int page, int pageSize)
    {
        await RequireTenantIdAsync(operatorUserId, WarehousePermissionCodes.TransferRead);
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var total = await db.Transfers.CountAsync();
        var transfers = await db.Transfers
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        var transferIds = transfers.Select(t => t.Id).ToList();
        var items = await db.TransferItems
            .Where(i => transferIds.Contains(i.TransferId))
            .ToListAsync();

        return new PageResultTransfers(
            transfers.Select(t => new BranchTransferDto(
                t.Id, t.TransferNo, t.FromBranchId, t.ToBranchId, t.Status.ToString(),
                items.Where(i => i.TransferId == t.Id).OrderBy(i => i.Seq).Select(i =>
                    new BranchTransferItemDto(
                        i.Id, i.Seq, i.CargoName, i.DeclaredQuantity,
                        i.ReceivedQuantity, i.BrokenQuantity, i.Remark)).ToList(),
                t.Note, t.CreatedAt, t.SubmittedAt, t.DispatchedAt, t.ReceivedAt)).ToList(),
            total, page, pageSize);
    }

    // ---------- 辅助 ----------

    private async Task<Guid> RequireTenantIdAsync(Guid operatorUserId, string permissionCode)
    {
        if (db.CurrentTenantId is not { } tenantId)
        {
            throw new TenantContextMissingException();
        }

        if (!await authorization.HasPermissionAsync(operatorUserId, permissionCode))
        {
            throw new PermissionDeniedException(permissionCode);
        }

        return tenantId;
    }

    private async Task RequireFromBranchScopeAsync(Guid operatorUserId, BranchTransfer transfer)
    {
        if (!await authorization.AuthorizeAsync(
                operatorUserId, transfer.FromBranchId, WarehousePermissionCodes.TransferManage))
        {
            throw new PermissionDeniedException("warehouse.transfer（调出网点范围）");
        }
    }

    private async Task<BranchTransfer> RequireTransferAsync(Guid transferId)
    {
        // 包含清单：验收逐项校验依赖聚合明细
        var transfer = await db.Transfers
            .Include(t => t.Items)
            .SingleOrDefaultAsync(t => t.Id == transferId)
            ?? throw new EntityNotFoundException("调拨单", transferId);
        return transfer;
    }

    private async Task<string> RequireBranchCodeAsync(Guid tenantId, Guid branchId)
    {
        var branch = (await db.Database
                .SqlQuery<BranchCodeRow>(
                    $"""SELECT code AS "Code" FROM branches WHERE tenant_id = {tenantId} AND id = {branchId}""")
                .ToListAsync())
            .SingleOrDefault()
            ?? throw new EntityNotFoundException("网点", branchId);
        return branch.Code;
    }

    private Task RecordAuditAsync(string action, BranchTransfer transfer, Guid operatorUserId) =>
        audit.RecordAsync(action, "branch_transfer", transfer.Id, operatorUserId,
            new { transferNo = transfer.TransferNo, status = transfer.Status.ToString() });

    private async Task<BranchTransferDto> MapDetailAsync(BranchTransfer transfer)
    {
        var items = await db.TransferItems
            .Where(i => i.TransferId == transfer.Id)
            .OrderBy(i => i.Seq)
            .ToListAsync();

        return new BranchTransferDto(
            transfer.Id, transfer.TransferNo, transfer.FromBranchId, transfer.ToBranchId,
            transfer.Status.ToString(),
            items.Select(i => new BranchTransferItemDto(
                i.Id, i.Seq, i.CargoName, i.DeclaredQuantity,
                i.ReceivedQuantity, i.BrokenQuantity, i.Remark)).ToList(),
            transfer.Note, transfer.CreatedAt,
            transfer.SubmittedAt, transfer.DispatchedAt, transfer.ReceivedAt);
    }

    private sealed record StatusRow(short Status);

    private sealed record BranchCodeRow(string Code);
}
