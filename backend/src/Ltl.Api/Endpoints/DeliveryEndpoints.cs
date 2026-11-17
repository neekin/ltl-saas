using System.Security.Claims;
using Ltl.Modules.Delivery.Application;
using Ltl.Modules.Organization.Application.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ltl.Api.Endpoints;

/// <summary>交付端点（DELIVERY-001）。</summary>
public static class DeliveryEndpoints
{
    public static void MapDeliveryEndpoints(this WebApplication app)
    {
        var deliveries = app.MapGroup("/api/deliveries");
        deliveries.MapPost("/", async (CreateDeliveryRequest request, ClaimsPrincipal user, IDeliveryService service) =>
            Results.Ok(await service.CreateAsync(RequireUserId(user), request)));
        deliveries.MapGet("/", async (Guid waybillId, ClaimsPrincipal user, IDeliveryService service) =>
        {
            var result = await service.GetByWaybillAsync(RequireUserId(user), waybillId);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        deliveries.MapPost("/{id:guid}/complete", async (
            Guid id, CompleteDeliveryRequest request, ClaimsPrincipal user, IDeliveryService service) =>
            Results.Ok(await service.CompleteAsync(RequireUserId(user), id, request)));
        deliveries.MapPost("/{id:guid}/reject", async (Guid id, ClaimsPrincipal user, IDeliveryService service) =>
            Results.Ok(await service.RejectAsync(RequireUserId(user), id)));
    }

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new AuthenticationFailedException("无效的认证凭据");
}
