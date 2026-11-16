using System.Security.Claims;
using Ltl.Modules.Organization.Application.Auth;
using Ltl.Modules.Waybills.Application;
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
    }

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new AuthenticationFailedException("无效的认证凭据");
}
