using System.Security.Claims;
using Ltl.Modules.Organization.Application.Auth;
using Ltl.Modules.Waybills.Application;
using Ltl.Modules.Waybills.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ltl.Api.Endpoints;

/// <summary>运单端点（WB-PRINT：打印数据；后续开单/确认/取消端点随业务任务追加）。</summary>
public static class WaybillEndpoints
{
    public static void MapWaybillEndpoints(this WebApplication app)
    {
        app.MapGet("/api/waybills/{id:guid}/print", async (
            Guid id, ClaimsPrincipal user, IWaybillPrintService service) =>
            Results.Ok(await service.GetPrintViewAsync(RequireUserId(user), id)));

        // WB-003：查询/详情/草稿修改/确认/取消
        var waybills = app.MapGroup("/api/waybills");
        waybills.MapGet("/", async (
            int? page, int? pageSize, string? waybillNo, ClaimsPrincipal user, IWaybillAdminService service) =>
            Results.Ok(await service.ListAsync(RequireUserId(user), page ?? 1, pageSize ?? 20, waybillNo)));
        waybills.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IWaybillAdminService service) =>
            Results.Ok(await service.GetAsync(RequireUserId(user), id)));
        waybills.MapPut("/{id:guid}/draft", async (
            Guid id, UpdateWaybillDraftRequest request, ClaimsPrincipal user, IWaybillAdminService service) =>
            Results.Ok(await service.UpdateDraftAsync(RequireUserId(user), id, request)));
        waybills.MapPost("/{id:guid}/confirm", async (Guid id, ClaimsPrincipal user, IWaybillAdminService service) =>
            Results.Ok(await service.ConfirmAsync(RequireUserId(user), id)));
        waybills.MapPost("/{id:guid}/cancel", async (
            Guid id, CancelWaybillRequest request, ClaimsPrincipal user, IWaybillAdminService service) =>
            Results.Ok(await service.CancelAsync(RequireUserId(user), id, request.Reason)));
    }

    public sealed record CancelWaybillRequest(string? Reason);

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new AuthenticationFailedException("无效的认证凭据");
}
