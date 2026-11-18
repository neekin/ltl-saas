using System.Security.Claims;
using Ltl.Modules.Organization.Application.Auth;
using Ltl.Modules.Transport.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ltl.Api.Endpoints;

/// <summary>批次 GPS 轨迹端点（GPS-001）。</summary>
public static class GpsEndpoints
{
    public static void MapGpsEndpoints(this WebApplication app)
    {
        var gps = app.MapGroup("/api/transport/trips/{tripId:guid}/gps");
        gps.MapPost("/", async (
            Guid tripId, ReportGpsRequest request,
            ClaimsPrincipal user, ITripService service) =>
            Results.Ok(await service.ReportGpsAsync(RequireUserId(user), tripId, request)));
        gps.MapGet("/", async (
            Guid tripId, ClaimsPrincipal user, ITripService service) =>
            Results.Ok(await service.ListGpsAsync(RequireUserId(user), tripId)));
    }

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new AuthenticationFailedException("无效的认证凭据");
}
