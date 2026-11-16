using System.Security.Claims;
using Ltl.Modules.Organization.Application.Auth;
using Ltl.Modules.Warehouse.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ltl.Api.Endpoints;

/// <summary>仓储端点（RECEIVE-001：收货登记/查询）。</summary>
public static class WarehouseEndpoints
{
    public static void MapWarehouseEndpoints(this WebApplication app)
    {
        var receipts = app.MapGroup("/api/warehouse/receipts");

        receipts.MapPost("/", async (
            RecordCargoReceiptRequest request,
            ClaimsPrincipal user,
            ICargoReceiptService service) =>
            Results.Ok(await service.RecordAsync(
                RequireUserId(user), request.WaybillId, request.BranchId,
                request.ReceivedPieces, request.ReceivedWeightKg, request.ReceivedVolumeM3, request.Note)));

        receipts.MapGet("/", async (
            Guid waybillId, ClaimsPrincipal user, ICargoReceiptService service) =>
            Results.Ok(await service.ListByWaybillAsync(RequireUserId(user), waybillId)));
    }

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new AuthenticationFailedException("无效的认证凭据");
}

public sealed record RecordCargoReceiptRequest(
    Guid WaybillId, Guid BranchId, int ReceivedPieces,
    decimal ReceivedWeightKg, decimal ReceivedVolumeM3, string? Note);
