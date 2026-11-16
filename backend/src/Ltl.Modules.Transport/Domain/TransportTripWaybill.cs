namespace Ltl.Modules.Transport.Domain;

/// <summary>
/// 批次配载行（transport_trip_waybills）。装车确认留痕（loaded_at/by）；
/// active=true 表示该运单当前在一个有效（未到达）批次上——
/// 部分唯一索引 (tenant_id, waybill_id) WHERE active 数据库层强制防重复装车（TRIP-001 §2）。
/// </summary>
public sealed class TransportTripWaybill
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid TripId { get; private set; }

    public Guid WaybillId { get; private set; }

    public bool Active { get; private set; }

    public DateTime LoadedAt { get; private set; }

    public Guid? LoadedBy { get; private set; }

    private TransportTripWaybill()
    {
    }

    public static TransportTripWaybill Load(
        Guid tenantId, Guid tripId, Guid waybillId, DateTime loadedAtUtc, Guid? loadedBy)
    {
        if (loadedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("时间必须为 UTC。", nameof(loadedAtUtc));
        }

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("租户 Id 不能为空 Guid。", nameof(tenantId));
        }

        return new TransportTripWaybill
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TripId = tripId,
            WaybillId = waybillId,
            Active = true,
            LoadedAt = loadedAtUtc,
            LoadedBy = loadedBy,
        };
    }

    /// <summary>到达/撤销后置为非活跃（历史行保留）。</summary>
    public void MarkInactive() => Active = false;
}
