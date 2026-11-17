using System.Transactions;
using Ltl.Modules.Arrivals.Domain;
using Ltl.Modules.Arrivals.Infrastructure;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Platform.Application;
using Ltl.Modules.Transport.Domain;
using Ltl.Modules.Waybills.Application;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Arrivals.Application;

public sealed record ArrivalItemDto(
    Guid Id, Guid WaybillId, int LoadedPieces, int ReceivedPieces, int BrokenPieces, string? Remark);

public sealed record ArrivalDto(
    Guid Id, Guid TripId, Guid BranchId, IReadOnlyList<ArrivalItemDto> Items,
    Guid? OperatorUserId, string? Note, DateTime ArrivedAt);

public sealed record RecordArrivalRequest(
    Guid TripId, Guid BranchId,
    IReadOnlyList<ArrivalItemInput> Items, string? Note);

public sealed record ArrivalItemInput(
    Guid WaybillId, int ReceivedPieces, int BrokenPieces, string? Remark);

public sealed record ArrivalResult(
    ArrivalDto Arrival,
    IReadOnlyList<ArrivalItemDifference> Differences);

public sealed record ArrivalItemDifference(
    Guid WaybillId, int LoadedPieces, int ReceivedPieces, int BrokenPieces, int PieceDifference);

public interface IArrivalService
{
    Task<ArrivalResult> RecordAsync(Guid operatorUserId, RecordArrivalRequest request);
    Task<ArrivalDto?> GetByTripAsync(Guid operatorUserId, Guid tripId);
}

public sealed class ArrivalService(
    ArrivalDbContext db,
    IWaybillCargoService waybillCargoService,
    IAuthorizationService authorization,
    IAuditLogService audit) : IArrivalService
{
    public async Task<ArrivalResult> RecordAsync(Guid operatorUserId, RecordArrivalRequest request)
    {
        var tenantId = db.CurrentTenantId
            ?? throw new TenantContextMissingException();

        if (!await authorization.AuthorizeAsync(
                operatorUserId, request.BranchId, ArrivalPermissionCodes.ArrivalRecord))
        {
            throw new PermissionDeniedException(ArrivalPermissionCodes.ArrivalRecord);
        }

        var tripStatus = (await db.Database
                .SqlQuery<StatusRow>(
                    $"SELECT status AS \"Status\" FROM transport_trips WHERE tenant_id = {tenantId} AND id = {request.TripId}")
                .ToListAsync())
            .SingleOrDefault() ?? throw new EntityNotFoundException("运输批次", request.TripId);
        if (tripStatus.Status != (short)TransportTripStatus.Departed)
        {
            throw new InvalidOperationException(
                $"批次状态为 {(TransportTripStatus)tripStatus.Status}，仅已发车批次可到货登记。");
        }

        var waybillIds = request.Items.Select(i => i.WaybillId).ToList();
        var snapshots = new Dictionary<Guid, WaybillCargoSnapshot>();
        foreach (var waybillId in waybillIds)
        {
            snapshots[waybillId] = await waybillCargoService.GetSnapshotAsync(waybillId);
            if (snapshots[waybillId].TransportStatus != Ltl.Modules.Waybills.Domain.WaybillTransportStatus.Shipped)
            {
                throw new InvalidOperationException(
                    $"运单 {snapshots[waybillId].WaybillNo} 运输状态为 {snapshots[waybillId].TransportStatus}，仅已发运运单可到货登记。");
            }
        }

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        var arrival = CargoArrival.Create(
            tenantId, request.TripId, request.BranchId,
            request.Items.Select(i => (i.WaybillId, snapshots[i.WaybillId].DeclaredPieces, i.ReceivedPieces, i.BrokenPieces, i.Remark)).ToList(),
            operatorUserId, request.Note, DateTime.UtcNow, operatorUserId);
        db.Arrivals.Add(arrival);
        await db.SaveChangesAsync();

        foreach (var item in request.Items)
        {
            await waybillCargoService.MarkArrivedAtDestinationAsync(
                item.WaybillId, operatorUserId,
                $"批次到货：实收 {item.ReceivedPieces} 件 / 破损 {item.BrokenPieces} 件");
        }

        await audit.RecordAsync(
            "arrival.record", "arrival", arrival.Id, operatorUserId,
            new { tripId = request.TripId, branchId = request.BranchId, itemCount = request.Items.Count });
        scope.Complete();

        var differences = request.Items.Select(item => new ArrivalItemDifference(
            item.WaybillId,
            snapshots[item.WaybillId].DeclaredPieces,
            item.ReceivedPieces,
            item.BrokenPieces,
            item.ReceivedPieces - snapshots[item.WaybillId].DeclaredPieces)).ToList();

        return new ArrivalResult(ToDto(arrival), differences);
    }

    public async Task<ArrivalDto?> GetByTripAsync(Guid operatorUserId, Guid tripId)
    {
        if (db.CurrentTenantId is null)
            throw new TenantContextMissingException();
        if (!await authorization.HasPermissionAsync(operatorUserId, ArrivalPermissionCodes.ArrivalRead))
            throw new PermissionDeniedException(ArrivalPermissionCodes.ArrivalRead);

        var arrival = await db.Arrivals
            .Include(a => a.Items)
            .SingleOrDefaultAsync(a => a.TripId == tripId);
        return arrival is null ? null : ToDto(arrival);
    }

    private static ArrivalDto ToDto(CargoArrival arrival) => new(
        arrival.Id, arrival.TripId, arrival.BranchId,
        arrival.Items.Select(i => new ArrivalItemDto(
            i.Id, i.WaybillId, i.LoadedPieces, i.ReceivedPieces, i.BrokenPieces, i.Remark)).ToList(),
        arrival.OperatorUserId, arrival.Note, arrival.ArrivedAt);

    private sealed record StatusRow(short Status);
}
