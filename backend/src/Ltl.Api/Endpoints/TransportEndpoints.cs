using System.Security.Claims;
using Ltl.Modules.Organization.Application.Auth;
using Ltl.Modules.Transport.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ltl.Api.Endpoints;

/// <summary>运输档案端点（VEHICLE-001：车辆/司机）。</summary>
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
    }

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new AuthenticationFailedException("无效的认证凭据");
}
