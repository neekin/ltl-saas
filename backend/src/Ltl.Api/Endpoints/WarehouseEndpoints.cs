using System.Security.Claims;
using Ltl.Modules.Organization.Application.Auth;
using Ltl.Modules.Warehouse.Application;
using Ltl.Modules.Platform.Application;
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

        var sorting = app.MapGroup("/api/warehouse/sorting-tasks");
        sorting.MapPost("/", async (
            CreateSortingTaskRequest request, ClaimsPrincipal user, ICargoSortingService service) =>
            Results.Ok(await service.CreateAsync(
                RequireUserId(user), request.WaybillId, request.BranchId, request.Note)));
        sorting.MapPost("/{id:guid}/complete", async (Guid id, ClaimsPrincipal user, ICargoSortingService service) =>
            Results.Ok(await service.CompleteAsync(RequireUserId(user), id)));
        sorting.MapGet("/", async (
            Guid waybillId, ClaimsPrincipal user, ICargoSortingService service) =>
            Results.Ok(await service.ListByWaybillAsync(RequireUserId(user), waybillId)));

        var transfers = app.MapGroup("/api/warehouse/transfers");
        transfers.MapPost("/", async (CreateTransferRequest request, ClaimsPrincipal user, IBranchTransferService service) =>
            Results.Ok(await service.CreateAsync(RequireUserId(user), request)));
        transfers.MapGet("/", async (int? page, int? pageSize, ClaimsPrincipal user, IBranchTransferService service) =>
            Results.Ok(await service.ListAsync(RequireUserId(user), page ?? 1, pageSize ?? 20)));
        transfers.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IBranchTransferService service) =>
            Results.Ok(await service.GetAsync(RequireUserId(user), id)));
        transfers.MapPost("/{id:guid}/submit", async (Guid id, ClaimsPrincipal user, IBranchTransferService service) =>
            Results.Ok(await service.SubmitAsync(RequireUserId(user), id)));
        transfers.MapPost("/{id:guid}/dispatch", async (Guid id, ClaimsPrincipal user, IBranchTransferService service) =>
            Results.Ok(await service.DispatchAsync(RequireUserId(user), id)));
        transfers.MapPost("/{id:guid}/receive", async (Guid id, ReceiveTransferRequest request, ClaimsPrincipal user, IBranchTransferService service) =>
            Results.Ok(await service.ReceiveAsync(RequireUserId(user), id, request)));
        transfers.MapPost("/{id:guid}/cancel", async (Guid id, ClaimsPrincipal user, IBranchTransferService service) =>
            Results.Ok(await service.CancelAsync(RequireUserId(user), id)));
    }

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new AuthenticationFailedException("无效的认证凭据");
}

public sealed record RecordCargoReceiptRequest(
    Guid WaybillId, Guid BranchId, int ReceivedPieces,
    decimal ReceivedWeightKg, decimal ReceivedVolumeM3, string? Note);

public sealed record CreateSortingTaskRequest(Guid WaybillId, Guid BranchId, string? Note);


