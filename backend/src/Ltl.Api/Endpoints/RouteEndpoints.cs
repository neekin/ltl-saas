using System.Security.Claims;
using Ltl.Modules.Organization.Application.Auth;
using Ltl.Modules.Routing.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ltl.Api.Endpoints;

/// <summary>线路管理端点（ROUTE-002：线路/站点/运输方案）。</summary>
public static class RouteEndpoints
{
    public static void MapRouteEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/routes");
        routes.MapGet("/", async (int? page, int? pageSize, string? keyword, ClaimsPrincipal user, IRouteAdminService service) =>
            Results.Ok(await service.ListRoutesAsync(RequireUserId(user), page ?? 1, pageSize ?? 20, keyword)));
        routes.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IRouteAdminService service) =>
            Results.Ok(await service.GetRouteAsync(RequireUserId(user), id)));
        routes.MapPost("/", async (CreateRouteRequest request, ClaimsPrincipal user, IRouteAdminService service) =>
            Results.Ok(await service.CreateRouteAsync(RequireUserId(user), request)));
        routes.MapPut("/{id:guid}", async (Guid id, RenameRouteRequest request, ClaimsPrincipal user, IRouteAdminService service) =>
            Results.Ok(await service.RenameRouteAsync(RequireUserId(user), id, request.Name)));
        routes.MapPost("/{id:guid}/disable", async (Guid id, ClaimsPrincipal user, IRouteAdminService service) =>
            Results.Ok(await service.DisableRouteAsync(RequireUserId(user), id)));
        routes.MapPost("/{id:guid}/enable", async (Guid id, ClaimsPrincipal user, IRouteAdminService service) =>
            Results.Ok(await service.EnableRouteAsync(RequireUserId(user), id)));
        routes.MapPut("/{id:guid}/stops", async (Guid id, ReplaceStopsRequest request, ClaimsPrincipal user, IRouteAdminService service) =>
            Results.Ok(await service.ReplaceStopsAsync(RequireUserId(user), id, request.Stops)));
        routes.MapGet("/{id:guid}/plans", async (Guid id, ClaimsPrincipal user, IRouteAdminService service) =>
            Results.Ok(await service.ListPlansAsync(RequireUserId(user), id)));
        routes.MapPost("/{id:guid}/plans", async (Guid id, CreatePlanRequest request, ClaimsPrincipal user, IRouteAdminService service) =>
            Results.Ok(await service.CreatePlanAsync(RequireUserId(user), id, request)));
    }

    public sealed record RenameRouteRequest(string Name);

    public sealed record ReplaceStopsRequest(IReadOnlyList<RouteStopSpecDto> Stops);

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new AuthenticationFailedException("无效的认证凭据");
}
