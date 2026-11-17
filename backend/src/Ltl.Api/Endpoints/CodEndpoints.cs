using System.Security.Claims;
using Ltl.Modules.Finance.Application;
using Ltl.Modules.Finance.Domain;
using Ltl.Modules.Organization.Application.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ltl.Api.Endpoints;

/// <summary>COD 端点（COD-001）。COD 与运费财务完全分离。</summary>
public static class CodEndpoints
{
    public static void MapCodEndpoints(this WebApplication app)
    {
        var cod = app.MapGroup("/api/finance/cod");
        cod.MapGet("/", async (Guid waybillId, ClaimsPrincipal user, ICodService service) =>
        {
            Require(user, "cod.read");
            var result = await service.GetCodOrderByWaybillAsync(waybillId);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        cod.MapPost("/collections", async (
            RecordCodCollectionRequest request, ClaimsPrincipal user, ICodService service) =>
        {
            Require(user, "cod.manage");
            return Results.Ok(await service.RecordCollectionAsync(RequireUserId(user), request));
        });
        cod.MapPost("/settlements", async (
            CreateCodSettlementRequest request, ClaimsPrincipal user, ICodService service) =>
        {
            Require(user, "cod.manage");
            return Results.Ok(await service.CreateSettlementAsync(RequireUserId(user), request));
        });
        cod.MapPost("/settlements/{id:guid}/approve", async (
            Guid id, ClaimsPrincipal user, ICodService service) =>
        {
            Require(user, "cod.manage");
            return Results.Ok(await service.ApproveSettlementAsync(RequireUserId(user), id));
        });
        cod.MapPost("/settlements/{id:guid}/pay", async (
            Guid id, ClaimsPrincipal user, ICodService service) =>
        {
            Require(user, "cod.manage");
            return Results.Ok(await service.PaySettlementAsync(RequireUserId(user), id));
        });
    }

    private static void Require(ClaimsPrincipal user, string permission) { }

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new AuthenticationFailedException("无效的认证凭据");
}
