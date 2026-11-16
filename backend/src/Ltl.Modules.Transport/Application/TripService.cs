using System.Transactions;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Platform.Application;
using Ltl.Modules.Platform.Domain;
using Ltl.Modules.Transport.Domain;
using Ltl.Modules.Transport.Infrastructure;
using Ltl.Modules.Transport.Domain;
using Ltl.Modules.Waybills.Application;
using Ltl.Modules.Waybills.Domain;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ltl.Modules.Transport.Application;

public sealed record TransportTripDto(
    Guid Id, string TripNo, Guid OriginBranchId, Guid DestBranchId, string Status,
    Guid VehicleId, Guid? DriverId, Guid? RouteId, string? Note, DateTime CreatedAt,
    IReadOnlyList<TripWaybillDto> Waybills);

public sealed record TripWaybillDto(
    Guid Id, Guid WaybillId, bool Active, DateTime LoadedAt, Guid? LoadedBy);

public sealed record CreateTripRequest(
    Guid OriginBranchId, Guid DestBranchId, Guid VehicleId, Guid? DriverId, Guid? RouteId, string? Note);

public sealed record LoadWaybillRequest(Guid WaybillId);

/// <summary>运输批次/配载服务（TRIP-001）。发车/到达/运输费用属 TRIP-002。</summary>
public interface ITripService
{
    Task<TransportTripDto> CreateTripAsync(Guid operatorUserId, CreateTripRequest request);
    Task<TransportTripDto> GetTripAsync(Guid operatorUserId, Guid tripId);
    Task<TripWaybillDto> LoadWaybillAsync(Guid operatorUserId, Guid tripId, Guid waybillId);
    Task RemoveWaybillAsync(Guid operatorUserId, Guid tripId, Guid assignmentId);
    Task<IReadOnlyList<TripWaybillDto>> ListWaybillTripsAsync(Guid operatorUserId, Guid waybillId);
}

/// <summary>
/// 运输批次实现（TRIP-001）。批次号 SEQ-001；配载校验货物状态（已收货/已分拣）与车辆容量；
/// 防重复装车由部分唯一索引强制（并发冲突 → 409）。
/// </summary>
public sealed class TripService(
    TransportDbContext db,
    IBranchSequenceService branchSequenceService,
    IWaybillCargoService waybillCargoService,
    IAuthorizationService authorization,
    IAuditLogService audit) : ITripService
{
    public async Task<TransportTripDto> CreateTripAsync(Guid operatorUserId, CreateTripRequest request)
    {
        var tenantId = await RequireTenantIdAsync(operatorUserId, TransportPermissionCodes.TripManage);

        // 车辆必须为本租户启用车辆（原生 SQL 显式租户参数，AGENTS §5.1）
        var vehicleActive = await DbVehicleActiveAsync(tenantId, request.VehicleId);
        if (!vehicleActive)
        {
            throw new EntityNotFoundException("车辆", request.VehicleId);
        }

        if (request.DriverId is { } driverId)
        {
            var driverActive = await DbDriverActiveAsync(tenantId, driverId);
            if (!driverActive)
            {
                throw new EntityNotFoundException("司机", driverId);
            }
        }

        // 始发网点码（批次号组成，服务端解析）
        var originCode = await RequireBranchCodeAsync(tenantId, request.OriginBranchId);
        await RequireBranchCodeAsync(tenantId, request.DestBranchId);

        if (request.RouteId is { } routeId)
        {
            var routeExists = (await db.Database
                    .SqlQuery<ExistsRow>(
                        $"""SELECT 1 AS "Exists" FROM routes WHERE tenant_id = {tenantId} AND id = {routeId}""")
                    .ToListAsync())
                .Any();
            if (!routeExists)
            {
                throw new EntityNotFoundException("线路", routeId);
            }
        }

        var tripNo = await branchSequenceService.NextNumberAsync(
            request.OriginBranchId, BusinessDocumentType.TransportTrip,
            DateOnly.FromDateTime(DateTime.UtcNow), originCode);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var trip = TransportTrip.Create(
            tenantId, tripNo, request.OriginBranchId, request.DestBranchId,
            request.VehicleId, request.DriverId, request.RouteId, request.Note,
            DateTime.UtcNow, operatorUserId);
        // 无导航关系的 FK 仅在数据库层：先存主体（批次）再存区段（ORG-001 规范，防插入顺序 FK 违反）
        db.Trips.Add(trip);
        await db.SaveChangesAsync();
        db.TripSegments.Add(TransportTripSegment.Create(
            tenantId, trip.Id, 1, trip.OriginBranchId, trip.DestBranchId, DateTime.UtcNow));
        await db.SaveChangesAsync();

        await audit.RecordAsync(
            "transport.trip.create", "transport_trip", trip.Id, operatorUserId,
            new { tripNo = trip.TripNo, originBranchId = trip.OriginBranchId, destBranchId = trip.DestBranchId });
        scope.Complete();

        return ToDto(trip);
    }

    public async Task<TransportTripDto> GetTripAsync(Guid operatorUserId, Guid tripId)
    {
        var trip = await RequireTripAsync(tripId);
        var assignments = await db.TripWaybills
            .Where(w => w.TripId == tripId)
            .OrderBy(w => w.LoadedAt)
            .ToListAsync();
        return ToDto(trip, assignments);
    }

    public async Task<TripWaybillDto> LoadWaybillAsync(Guid operatorUserId, Guid tripId, Guid waybillId)
    {
        var tenantId = await RequireTenantIdAsync(operatorUserId, TransportPermissionCodes.TripManage);
        var trip = await RequireTripAsync(tripId);

        if (trip.Status != TransportTripStatus.Planned)
        {
            throw new TransportDomainException($"批次 {trip.TripNo} 已发运，不可配载。");
        }

        // 货物状态守卫：已收货或已分拣可装车（快照经 Waybills 公开接口）
        var snapshot = await waybillCargoService.GetSnapshotAsync(waybillId);
        if (snapshot.CargoStatus is not (WaybillCargoStatus.Received or WaybillCargoStatus.Sorted))
        {
            throw new TransportDomainException(
                $"运单 {snapshot.WaybillNo} 货物状态为 {snapshot.CargoStatus}，仅已收货/已分拣运单可装车。");
        }

        // 车辆容量：累计申报重量/体积 ≤ 上限（含本批次已配载）
        var vehicle = await db.Vehicles.SingleAsync(v => v.Id == trip.VehicleId);
        var existing = await db.TripWaybills
            .Where(w => w.TripId == tripId && w.Active)
            .ToListAsync();
        var snapshotWaybillIds = existing.Select(w => w.WaybillId).Append(waybillId).ToList();
        var declaredWeights = new List<decimal>();
        var declaredVolumes = new List<decimal>();
        foreach (var assignedWaybillId in snapshotWaybillIds)
        {
            var declared = await waybillCargoService.GetSnapshotAsync(assignedWaybillId);
            declaredWeights.Add(declared.DeclaredWeightKg);
            declaredVolumes.Add(declared.DeclaredVolumeM3);
        }

        if (declaredWeights.Sum() > vehicle.MaxLoadKg)
        {
            throw new ConflictException(
                $"超出车辆最大载重（{vehicle.MaxLoadKg}kg）：本批次累计申报 {declaredWeights.Sum()}kg。");
        }

        if (declaredVolumes.Sum() > vehicle.MaxVolumeM3)
        {
            throw new ConflictException(
                $"超出车辆最大容积（{vehicle.MaxVolumeM3}m³）：本批次累计申报 {declaredVolumes.Sum()}m³。");
        }

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var assignment = TransportTripWaybill.Load(tenantId, tripId, waybillId, DateTime.UtcNow, operatorUserId);
        db.TripWaybills.Add(assignment);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "ux_transport_trip_waybills_active_waybill",
            })
        {
            // 防重复装车（并发兜底）：该运单已在另一有效批次上
            throw new ConflictException($"运单已在另一个有效运输批次上，不可重复装车。");
        }

        await audit.RecordAsync(
            "transport.trip.load_waybill", "transport_trip_waybill", assignment.Id, operatorUserId,
            new { tripNo = trip.TripNo, waybillNo = snapshot.WaybillNo });
        scope.Complete();

        return new TripWaybillDto(
            assignment.Id, assignment.WaybillId, assignment.Active, assignment.LoadedAt, assignment.LoadedBy);
    }

    public async Task RemoveWaybillAsync(Guid operatorUserId, Guid tripId, Guid assignmentId)
    {
        await RequireTenantIdAsync(operatorUserId, TransportPermissionCodes.TripManage);
        var trip = await RequireTripAsync(tripId);
        var assignment = await db.TripWaybills.FindAsync([assignmentId])
            ?? throw new EntityNotFoundException("配载记录", assignmentId);

        if (assignment.TripId != tripId)
        {
            throw new EntityNotFoundException("配载记录", assignmentId);
        }

        if (trip.Status != TransportTripStatus.Planned)
        {
            throw new TransportDomainException($"批次 {trip.TripNo} 已发运，不可撤销配载。");
        }

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        db.TripWaybills.Remove(assignment);
        await db.SaveChangesAsync();
        await audit.RecordAsync(
            "transport.trip.remove_waybill", "transport_trip_waybill", assignmentId, operatorUserId,
            new { tripNo = trip.TripNo, waybillId = assignment.WaybillId });
        scope.Complete();
    }

    public async Task<IReadOnlyList<TripWaybillDto>> ListWaybillTripsAsync(Guid operatorUserId, Guid waybillId)
    {
        if (!await authorization.HasPermissionAsync(operatorUserId, TransportPermissionCodes.TripRead))
        {
            throw new PermissionDeniedException(TransportPermissionCodes.TripRead);
        }

        if (db.CurrentTenantId is null)
        {
            throw new TenantContextMissingException();
        }

        return (await db.TripWaybills
                .Where(w => w.WaybillId == waybillId)
                .OrderBy(w => w.LoadedAt)
                .ToListAsync())
            .Select(w => new TripWaybillDto(w.Id, w.WaybillId, w.Active, w.LoadedAt, w.LoadedBy))
            .ToList();
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

    private async Task<TransportTrip> RequireTripAsync(Guid tripId) =>
        await db.Trips.FindAsync([tripId]) ?? throw new EntityNotFoundException("运输批次", tripId);

    private async Task<bool> DbVehicleActiveAsync(Guid tenantId, Guid vehicleId) =>
        (await db.Database
            .SqlQuery<StatusRow>(
                $"""SELECT status AS "Status" FROM vehicles WHERE tenant_id = {tenantId} AND id = {vehicleId}""")
            .ToListAsync())
        .SingleOrDefault() is { Status: 0 };

    private async Task<bool> DbDriverActiveAsync(Guid tenantId, Guid driverId) =>
        (await db.Database
            .SqlQuery<StatusRow>(
                $"""SELECT status AS "Status" FROM drivers WHERE tenant_id = {tenantId} AND id = {driverId}""")
            .ToListAsync())
        .SingleOrDefault() is { Status: 0 };

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

    private sealed record StatusRow(short Status);

    private sealed record BranchCodeRow(string Code);

    private sealed record ExistsRow(bool Exists);

    private static TransportTripDto ToDto(TransportTrip trip, IReadOnlyList<TransportTripWaybill>? assignments = null) =>
        new(
            trip.Id, trip.TripNo, trip.OriginBranchId, trip.DestBranchId, trip.Status.ToString(),
            trip.VehicleId, trip.DriverId, trip.RouteId, trip.Note, trip.CreatedAt,
            assignments?.Select(w => new TripWaybillDto(
                w.Id, w.WaybillId, w.Active, w.LoadedAt, w.LoadedBy)).ToList() ?? []);
}
