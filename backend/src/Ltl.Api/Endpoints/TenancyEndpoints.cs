using System.Security.Claims;
using Ltl.Modules.Organization.Application.Auth;
using Ltl.Modules.Tenancy.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ltl.Api.Endpoints;

/// <summary>租户配额端点（TEN-002）。</summary>
public static class TenancyEndpoints
{
    public static void MapTenancyEndpoints(this WebApplication app)
    {
        var quota = app.MapGroup("/api/tenancy/quota");
        quota.MapGet("/", async (ClaimsPrincipal user, ITenantQuotaService service) =>
            Results.Ok(await service.GetAsync()));
        quota.MapPut("/", async (SetTenantQuotaRequest request, ClaimsPrincipal user, ITenantQuotaService service) =>
            {
                await service.SetQuotaAsync(RequireUserId(user), request);
                return Results.Ok(new { updated = true });
            });
    }

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new AuthenticationFailedException("无效的认证凭据");
}
