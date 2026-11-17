using System.Security.Claims;
using Ltl.Modules.Organization.Application.Auth;
using Ltl.Modules.Reporting.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ltl.Api.Endpoints;

/// <summary>经营看板端点（REPORT-001）。</summary>
public static class ReportingEndpoints
{
    public static void MapReportingEndpoints(this WebApplication app)
    {
        app.MapGet("/api/reports/dashboard", async (
            string? month, ClaimsPrincipal user, IReportingService service) =>
            Results.Ok(await service.GetDashboardAsync(RequireUserId(user), month)));
    }

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new AuthenticationFailedException("无效的认证凭据");
}
