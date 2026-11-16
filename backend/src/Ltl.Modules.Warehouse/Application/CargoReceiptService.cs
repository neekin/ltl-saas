using System.Transactions;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Platform.Application;
using Ltl.Modules.Warehouse.Domain;
using Ltl.Modules.Warehouse.Infrastructure;
using Ltl.Modules.Waybills.Application;
using Ltl.Modules.Waybills.Domain;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Warehouse.Application;

public sealed record CargoReceiptDto(
    Guid Id, Guid WaybillId, Guid BranchId, int ReceivedPieces,
    decimal ReceivedWeightKg, decimal ReceivedVolumeM3, Guid? OperatorUserId, string? Note, DateTime OccurredAt);

/// <summary>收货登记响应：含申报 vs 实收差异（申报口径来自运单聚合 Σ货物件）。</summary>
public sealed record CargoReceiptResult(
    CargoReceiptDto Receipt,
    WaybillCargoSnapshot Waybill,
    int PieceDifference,
    decimal WeightDifferenceKg,
    decimal VolumeDifferenceM3);

public interface ICargoReceiptService
{
    /// <summary>登记收货（组合授权 + 状态守卫 + 记录/事件/审计同环境事务）。</summary>
    Task<CargoReceiptResult> RecordAsync(
        Guid operatorUserId, Guid waybillId, Guid branchId,
        int receivedPieces, decimal receivedWeightKg, decimal receivedVolumeM3, string? note);

    /// <summary>运单收货记录列表（时间正序）。</summary>
    Task<IReadOnlyList<CargoReceiptDto>> ListByWaybillAsync(Guid operatorUserId, Guid waybillId);
}

/// <summary>
/// 收货登记实现（RECEIVE-001）。组合授权（权限点 + 收货网点范围，实时查库）；
/// 状态流转经 Waybills 公开接口（聚合守卫）；记录/事件/审计同库环境事务（ORG-003 样板）。
/// </summary>
public sealed class CargoReceiptService(
    WarehouseDbContext db,
    IWaybillCargoService waybillCargoService,
    IAuthorizationService authorization,
    IAuditLogService audit) : ICargoReceiptService
{
    public async Task<CargoReceiptResult> RecordAsync(
        Guid operatorUserId, Guid waybillId, Guid branchId,
        int receivedPieces, decimal receivedWeightKg, decimal receivedVolumeM3, string? note)
    {
        var tenantId = db.CurrentTenantId
            ?? throw new TenantContextMissingException();

        // 组合授权：权限点 + 收货网点数据范围（AGENTS §5.2，实时查库）
        if (!await authorization.AuthorizeAsync(operatorUserId, branchId, WarehousePermissionCodes.CargoReceive))
        {
            throw new PermissionDeniedException("warehouse.cargo.receive");
        }

        // 申报口径快照（读取，无需事务）
        var snapshot = await waybillCargoService.GetSnapshotAsync(waybillId);
        var transitionRequired = snapshot.CargoStatus == WaybillCargoStatus.PendingReceive;

        // 环境事务先行：状态流转（Waybills）、收货记录（Warehouse）、审计（Platform）
        // 三个 DbContext 同库 enlist——任一失败整体回滚（RECEIVE-001 设计 §4）
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        if (transitionRequired)
        {
            snapshot = await waybillCargoService.MarkCargoReceivedAsync(
                waybillId, operatorUserId,
                $"收货登记：实收 {receivedPieces} 件 / {receivedWeightKg}kg / {receivedVolumeM3}m³（网点 {branchId}）");
        }

        var receipt = CargoReceipt.Create(
            tenantId, waybillId, branchId, receivedPieces, receivedWeightKg, receivedVolumeM3,
            operatorUserId, note, DateTime.UtcNow, operatorUserId);
        db.Receipts.Add(receipt);
        await db.SaveChangesAsync();

        await audit.RecordAsync(
            "warehouse.cargo.receive", "cargo_receipt", receipt.Id, operatorUserId,
            new { waybillNo = snapshot.WaybillNo, branchId, receivedPieces, receivedWeightKg, receivedVolumeM3 });
        scope.Complete();

        return new CargoReceiptResult(
            ToDto(receipt), snapshot,
            receivedPieces - snapshot.DeclaredPieces,
            receivedWeightKg - snapshot.DeclaredWeightKg,
            receivedVolumeM3 - snapshot.DeclaredVolumeM3);
    }

    public async Task<IReadOnlyList<CargoReceiptDto>> ListByWaybillAsync(Guid operatorUserId, Guid waybillId)
    {
        await RequirePermissionAsync(operatorUserId, WarehousePermissionCodes.CargoRead);
        return (await db.Receipts
                .Where(r => r.WaybillId == waybillId)
                .OrderBy(r => r.OccurredAt)
                .ToListAsync())
            .Select(ToDto)
            .ToList();
    }

    private async Task RequirePermissionAsync(Guid operatorUserId, string permissionCode)
    {
        if (!await authorization.HasPermissionAsync(operatorUserId, permissionCode))
        {
            throw new PermissionDeniedException(permissionCode);
        }
    }

    private static CargoReceiptDto ToDto(CargoReceipt receipt) => new(
        receipt.Id, receipt.WaybillId, receipt.BranchId, receipt.ReceivedPieces,
        receipt.ReceivedWeightKg, receipt.ReceivedVolumeM3, receipt.OperatorUserId,
        receipt.Note, receipt.OccurredAt);
}
