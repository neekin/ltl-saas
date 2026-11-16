using System.Security.Claims;
using Ltl.Modules.Organization.Application.Auth;
using Ltl.Modules.Transport.Application;
using Ltl.Modules.Waybills.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ltl.Api.Endpoints;

/// <summary>运输端点（VEHICLE-001 档案 + TRIP-001 批次/配载）。</summary>
public static class TransportEndpoints
{
    public static void MapTransportEndpoints(this WebApplication app)
    {
        var vehicles = app.MapGroup("/api/transport/vehicles");
        vehicles.MapGet("/", async (int? page, int? pageSize, ClaimsPrincipal user, IFleetAdminService service) =>
            Results.Ok(await service.ListVehiclesAsync(RequireUserId(user), page ?? 1, pageSize ?? 20)));
        vehicles.MapPost("/", async (CreateVehicleRequest request, ClaimsPrincipal user, IFleetAdminService service) =>
            Results.Ok(await service.CreateVehicleAsync(RequireUserId(user), request)));
        vehicles.MapPut("/{id:guid}", async (Guid id, UpdateVehicleRequest request, ClaimsPrincipal user, IFleetAdminService service) =>
            Results.Ok(await service.UpdateVehicleAsync(RequireUserId(user), id, request)));
        vehicles.MapPost("/{id:guid}/disable", async (Guid id, ClaimsPrincipal user, IFleetAdminService service) =>
            Results.Ok(await service.DisableVehicleAsync(RequireUserId(user), id)));
        vehicles.MapPost("/{id:guid}/enable", async (Guid id, ClaimsPrincipal user, IFleetAdminService service) =>
            Results.Ok(await service.EnableVehicleAsync(RequireUserId(user), id)));

        var drivers = app.MapGroup("/api/transport/drivers");
        drivers.MapGet("/", async (int? page, int? pageSize, ClaimsPrincipal user, IFleetAdminService service) =>
            Results.Ok(await service.ListDriversAsync(RequireUserId(user), page ?? 1, pageSize ?? 20)));
        drivers.MapPost("/", async (CreateDriverRequest request, ClaimsPrincipal user, IFleetAdminService service) =>
            Results.Ok(await service.CreateDriverAsync(RequireUserId(user), request)));
        drivers.MapPut("/{id:guid}", async (Guid id, UpdateDriverRequest request, ClaimsPrincipal user, IFleetAdminService service) =>
            Results.Ok(await service.UpdateDriverAsync(RequireUserId(user), id, request)));
        drivers.MapPost("/{id:guid}/disable", async (Guid id, ClaimsPrincipal user, IFleetAdminService service) =>
            Results.Ok(await service.DisableDriverAsync(RequireUserId(user), id)));
        drivers.MapPost("/{id:guid}/enable", async (Guid id, ClaimsPrincipal user, IFleetAdminService service) =>
            Results.Ok(await service.EnableDriverAsync(RequireUserId(user), id)));

        var trips = app.MapGroup("/api/transport/trips");
        trips.MapPost("/", async (CreateTripRequest request, ClaimsPrincipal user, ITripService service) =>
            Results.Ok(await service.CreateTripAsync(RequireUserId(user), request)));
        trips.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, ITripService service) =>
            Results.Ok(await service.GetTripAsync(RequireUserId(user), id)));
        trips.MapPost("/{id:guid}/waybills", async (
            Guid id, LoadWaybillRequest request, ClaimsPrincipal user, ITripService service) =>
            Results.Ok(await service.LoadWaybillAsync(RequireUserId(user), id, request.WaybillId)));
        trips.MapDelete("/{id:guid}/waybills/{assignmentId:guid}", async (
            Guid id, Guid assignmentId, ClaimsPrincipal user, ITripService service) =>
        {
            await service.RemoveWaybillAsync(RequireUserId(user), id, assignmentId);
            return Results.Ok(new { removed = true });
        });

        // 运单运输批次历史（关联历史）
        app.MapGet("/api/waybills/{id:guid}/trips", async (
            Guid id, ClaimsPrincipal user, ITripService service) =>
            Results.Ok(await service.ListWaybillTripsAsync(RequireUserId(user), id)));

        // TRIP-002：发车/到达/运输费用
        trips.MapGet("/", async (int? page, int? pageSize, ClaimsPrincipal user, ITripService service) =>
            Results.Ok(await service.ListTripsAsync(RequireUserId(user), page ?? 1, pageSize ?? 20)));
        trips.MapPost("/{id:guid}/depart", async (Guid id, ClaimsPrincipal user, ITripService service) =>
            Results.Ok(await service.DepartTripAsync(RequireUserId(user), id)));
        trips.MapPost("/{id:guid}/arrive", async (Guid id, ClaimsPrincipal user, ITripService service) =>
            Results.Ok(await service.ArriveTripAsync(RequireUserId(user), id)));
        trips.MapPost("/{id:guid}/fees", async (
            Guid id, RecordTripFeeRequest request, ClaimsPrincipal user, ITripService service) =>
            Results.Ok(await service.RecordFeeAsync(
                RequireUserId(user), id, request.FeeType, request.Amount, request.Note)));
        trips.MapGet("/{id:guid}/fees", async (Guid id, ClaimsPrincipal user, ITripService service) =>
            Results.Ok(await service.ListFeesAsync(RequireUserId(user), id)));
    }

    public sealed record RecordTripFeeRequest(string FeeType, decimal Amount, string? Note);

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new AuthenticationFailedException("无效的认证凭据");
}
