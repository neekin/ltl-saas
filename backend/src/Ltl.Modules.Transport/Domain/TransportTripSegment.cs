namespace Ltl.Modules.Transport.Domain;

/// <summary>
/// 运输批次实际区段（transport_trip_segments）。创建批次自动生成 seq=1（起点→终点）；
/// 多段历史随到达扩展（TRIP-002）。
/// </summary>
public sealed class TransportTripSegment
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid TripId { get; private set; }

    public int Seq { get; private set; }

    public Guid FromBranchId { get; private set; }

    public Guid ToBranchId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private TransportTripSegment()
    {
    }

    public static TransportTripSegment Create(
        Guid tenantId, Guid tripId, int seq, Guid fromBranchId, Guid toBranchId, DateTime createdAtUtc)
    {
        if (seq < 1)
        {
            throw new ArgumentException("区段序号必须 ≥1。", nameof(seq));
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("时间必须为 UTC。", nameof(createdAtUtc));
        }

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("租户 Id 不能为空 Guid。", nameof(tenantId));
        }

        return new TransportTripSegment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TripId = tripId,
            Seq = seq,
            FromBranchId = fromBranchId,
            ToBranchId = toBranchId,
            CreatedAt = createdAtUtc,
        };
    }
}
