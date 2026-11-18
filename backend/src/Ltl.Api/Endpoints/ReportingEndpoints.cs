using System.Security.Claims;
using Ltl.Modules.Organization.Application.Auth;
using Ltl.Modules.Reporting.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ltl.Api.Endpoints;

/// <summary>经营看板与维度报表端点（REPORT-001 / REPORT-002）。</summary>
public static class ReportingEndpoints
{
    public static void MapReportingEndpoints(this WebApplication app)
    {
        app.MapGet("/api/reports/dashboard", async (
            string? month, ClaimsPrincipal user, IReportingService service) =>
            Results.Ok(await service.GetDashboardAsync(RequireUserId(user), month)));

        app.MapGet("/api/reports/branches", async (
            string? month, ClaimsPrincipal user, IReportingService service) =>
            Results.Ok(await service.GetBranchReportAsync(RequireUserId(user), month)));

        app.MapGet("/api/reports/routes", async (
            string? month, ClaimsPrincipal user, IReportingService service) =>
            Results.Ok(await service.GetRouteReportAsync(RequireUserId(user), month)));

        app.MapGet("/api/reports/customers", async (
            string? month, ClaimsPrincipal user, IReportingService service) =>
            Results.Ok(await service.GetCustomerReportAsync(RequireUserId(user), month)));

        // P10：租户用量统计（仅展示，不做配额强制）
        app.MapGet("/api/reports/usage", async (ClaimsPrincipal user, IReportingService service) =>
            Results.Ok(await service.GetTenantUsageAsync(RequireUserId(user))));
    }

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new AuthenticationFailedException("无效的认证凭据");
}
