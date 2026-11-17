using System.Security.Claims;
using Ltl.Modules.Arrivals.Application;
using Ltl.Modules.Organization.Application.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ltl.Api.Endpoints;

/// <summary>到货登记端点（ARRIVAL-001）。</summary>
public static class ArrivalEndpoints
{
    public static void MapArrivalEndpoints(this WebApplication app)
    {
        var arrivals = app.MapGroup("/api/arrivals");
        arrivals.MapPost("/", async (RecordArrivalRequest request, ClaimsPrincipal user, IArrivalService service) =>
            Results.Ok(await service.RecordAsync(RequireUserId(user), request)));
        arrivals.MapGet("/", async (Guid tripId, ClaimsPrincipal user, IArrivalService service) =>
            Results.Ok(await service.GetByTripAsync(RequireUserId(user), tripId)));
    }

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new AuthenticationFailedException("无效的认证凭据");
}
