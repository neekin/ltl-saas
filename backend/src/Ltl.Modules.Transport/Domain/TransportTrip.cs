namespace Ltl.Modules.Transport.Domain;

/// <summary>
/// 运输批次状态（transport_trips.status）。TRIP-001 仅 Planned；发车/到达随 TRIP-002 扩展。
/// </summary>
public enum TransportTripStatus
{
    /// <summary>已计划（可配载）。</summary>
    Planned = 1,
}

/// <summary>
/// 运输批次（一次实际运输任务，AGENTS §6.2：TransportTrip ≠ Route 定义）。
/// 批次号 SEQ-001 生成（PC + 始发网点码 + 期间 + 序号）。
/// </summary>
public sealed class TransportTrip : TenantScopedEntity
{
    public string TripNo { get; private set; } = null!;

    public Guid? RouteId { get; private set; }

    public Guid VehicleId { get; private set; }

    public Guid? DriverId { get; private set; }

    public Guid OriginBranchId { get; private set; }

    public Guid DestBranchId { get; private set; }

    public TransportTripStatus Status { get; private set; }

    public string? Note { get; private set; }

    private TransportTrip()
    {
    }

    public static TransportTrip Create(
        Guid tenantId, string tripNo, Guid originBranchId, Guid destBranchId,
        Guid vehicleId, Guid? driverId, Guid? routeId, string? note,
        DateTime nowUtc, Guid? createdBy = null)
    {
        if (originBranchId == Guid.Empty || destBranchId == Guid.Empty)
        {
            throw new ArgumentException("起点/终点网点 Id 不能为空 Guid。");
        }

        if (originBranchId == destBranchId)
        {
            throw new ArgumentException("运输批次起点与终点不能是同一网点。");
        }

        if (vehicleId == Guid.Empty)
        {
            throw new ArgumentException("车辆 Id 不能为空 Guid。");
        }

        return new TransportTrip
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            TripNo = RequireNonBlank(tripNo, "批次号", 64),
            RouteId = routeId,
            VehicleId = vehicleId,
            DriverId = driverId,
            OriginBranchId = originBranchId,
            DestBranchId = destBranchId,
            Status = TransportTripStatus.Planned,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAt = EnsureUtc(nowUtc),
            CreatedBy = createdBy,
        };
    }
}
