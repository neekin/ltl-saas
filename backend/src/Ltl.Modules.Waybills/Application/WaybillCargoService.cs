using Ltl.Modules.Waybills.Domain;
using Ltl.Modules.Waybills.Infrastructure;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Waybills.Application;

/// <summary>运单申报口径快照（收货/分拣差异比对用）。</summary>
public sealed record WaybillCargoSnapshot(
    Guid WaybillId,
    string WaybillNo,
    WaybillStatus Status,
    WaybillCargoStatus CargoStatus,
    WaybillTransportStatus TransportStatus,
    WaybillDeliveryStatus DeliveryStatus,
    int DeclaredPieces,
    decimal DeclaredWeightKg,
    decimal DeclaredVolumeM3);

/// <summary>
/// 运单货物流转公开接口（RECEIVE-001/WAREHOUSE-001，Warehouse 模块调用）。
/// 状态守卫与运单事件在聚合内完成；调用方经 TransactionScope 与仓储记录同事务。
/// </summary>
public interface IWaybillCargoService
{
    /// <summary>收货流转：仅已确认 + 待收货；成功后货物状态 Received 并追加运单事件。</summary>
    /// <exception cref="EntityNotFoundException">运单不存在或跨租户不可见。</exception>
    /// <exception cref="WaybillDomainException">状态机违规（409 语义）。</exception>
    Task<WaybillCargoSnapshot> MarkCargoReceivedAsync(Guid waybillId, Guid? operatorUserId, string detail);

    /// <summary>分拣流转：仅已收货；成功后货物状态 Sorted 并追加运单事件。</summary>
    Task<WaybillCargoSnapshot> MarkCargoSortedAsync(Guid waybillId, Guid? operatorUserId, string detail);

    /// <summary>运单申报口径快照（不改变状态；分批收货场景核对用）。</summary>
    Task<WaybillCargoSnapshot> GetSnapshotAsync(Guid waybillId);

    /// <summary>运输状态流转：发车标记（TRIP-002，未发运 → 已发运）+ 运单事件 Departed。</summary>
    Task MarkTransportShippedAsync(Guid waybillId, Guid? operatorUserId, string detail);

    /// <summary>运单事件 Arrived（TRIP-002；不改运输/交付状态）。</summary>
    Task MarkTransportArrivedAsync(Guid waybillId, Guid? operatorUserId, string detail);

    /// <summary>交付状态流转：到货（ARRIVAL-001，未发运守卫在聚合）+ 运单事件 Arrived。</summary>
    Task<WaybillCargoSnapshot> MarkArrivedAtDestinationAsync(Guid waybillId, Guid? operatorUserId, string detail);

    /// <summary>交付状态流转：签收（DELIVERY-001，WaitingPickup → Delivered）+ 运单事件。</summary>
    Task MarkDeliveredAsync(Guid waybillId, Guid? operatorUserId, string detail);

    /// <summary>交付状态流转：拒收（DELIVERY-001，WaitingPickup → Rejected）+ 运单事件。</summary>
    Task MarkRejectedAsync(Guid waybillId, Guid? operatorUserId, string detail);
}

public sealed class WaybillCargoService(WaybillsDbContext db) : IWaybillCargoService
{
    public async Task<WaybillCargoSnapshot> MarkCargoReceivedAsync(
        Guid waybillId, Guid? operatorUserId, string detail)
    {
        var waybill = await RequireWaybillAsync(waybillId);
        waybill.MarkCargoReceived(DateTime.UtcNow, operatorUserId);
        AppendEvent(waybill, WaybillEventType.Received, operatorUserId, detail);
        await db.SaveChangesAsync();
        return Snapshot(waybill);
    }

    public async Task<WaybillCargoSnapshot> MarkCargoSortedAsync(
        Guid waybillId, Guid? operatorUserId, string detail)
    {
        var waybill = await RequireWaybillAsync(waybillId);
        waybill.MarkCargoSorted(DateTime.UtcNow, operatorUserId);
        AppendEvent(waybill, WaybillEventType.Sorted, operatorUserId, detail);
        await db.SaveChangesAsync();
        return Snapshot(waybill);
    }

    public async Task<WaybillCargoSnapshot> GetSnapshotAsync(Guid waybillId) =>
        Snapshot(await RequireWaybillAsync(waybillId));

    public async Task MarkTransportShippedAsync(Guid waybillId, Guid? operatorUserId, string detail)
    {
        var waybill = await RequireWaybillAsync(waybillId);
        // 多段运输（AGENTS §1）：中转段再发车时运单仍处于运输中，只追加轨迹事件，不重复状态迁移。
        if (waybill.TransportStatus == WaybillTransportStatus.NotShipped)
        {
            waybill.MarkTransportShipped(DateTime.UtcNow, operatorUserId);
        }
        AppendEvent(waybill, WaybillEventType.Departed, operatorUserId, detail);
        await db.SaveChangesAsync();
    }

    public async Task MarkTransportArrivedAsync(Guid waybillId, Guid? operatorUserId, string detail)
    {
        var waybill = await RequireWaybillAsync(waybillId);
        AppendEvent(waybill, WaybillEventType.Arrived, operatorUserId, detail);
        await db.SaveChangesAsync();
    }

    public async Task<WaybillCargoSnapshot> MarkArrivedAtDestinationAsync(Guid waybillId, Guid? operatorUserId, string detail)
    {
        var waybill = await RequireWaybillAsync(waybillId);
        waybill.MarkArrivedAtDestination(DateTime.UtcNow, operatorUserId);
        AppendEvent(waybill, WaybillEventType.Arrived, operatorUserId, detail);
        await db.SaveChangesAsync();
        return Snapshot(waybill);
    }

    public async Task MarkDeliveredAsync(Guid waybillId, Guid? operatorUserId, string detail)
    {
        var waybill = await RequireWaybillAsync(waybillId);
        waybill.MarkDelivered(DateTime.UtcNow, operatorUserId);
        AppendEvent(waybill, WaybillEventType.Delivered, operatorUserId, detail);
        await db.SaveChangesAsync();
    }

    public async Task MarkRejectedAsync(Guid waybillId, Guid? operatorUserId, string detail)
    {
        var waybill = await RequireWaybillAsync(waybillId);
        waybill.MarkRejected(DateTime.UtcNow, operatorUserId);
        AppendEvent(waybill, WaybillEventType.RejectedDelivery, operatorUserId, detail);
        await db.SaveChangesAsync();
    }

    private async Task<Waybill> RequireWaybillAsync(Guid waybillId)
    {
        // 包含货物件：申报口径快照（Σ件数/重量/体积）依赖聚合明细
        var waybill = await db.Waybills
            .Include(w => w.Packages)
            .SingleOrDefaultAsync(w => w.Id == waybillId)
            ?? throw new EntityNotFoundException("运单", waybillId);
        return waybill;
    }

    private void AppendEvent(Waybill waybill, WaybillEventType eventType, Guid? operatorUserId, string detail) =>
        db.Events.Add(WaybillEvent.Create(
            waybill.TenantId, waybill.Id, eventType,
            fromStatus: null, waybill.Status,
            DateTime.UtcNow, branchId: null, operatorId: operatorUserId, detail, createdBy: operatorUserId));

    private static WaybillCargoSnapshot Snapshot(Waybill waybill) => new(
        waybill.Id, waybill.WaybillNo, waybill.Status, waybill.CargoStatus,
        waybill.TransportStatus,
        waybill.DeliveryStatus,
        waybill.TotalPieces, waybill.TotalWeightKg, waybill.TotalVolumeM3);
}
