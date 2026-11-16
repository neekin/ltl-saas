using System.Security.Claims;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Organization.Application.Auth;
using Ltl.Modules.Pricing.Application;
using Ltl.Modules.Pricing.Domain;
using Ltl.Modules.Pricing.Infrastructure;
using Ltl.SharedKernel.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ltl.Api.Endpoints;

/// <summary>
/// 计价管理端点（PRICE-003）：规则集/版本生命周期 + 试算。
/// 权限在端点层实时校验（pricing.read/manage）——计价服务同时被开单命令复用（服务层不重复校验）。
/// </summary>
public static class PricingEndpoints
{
    public static void MapPricingEndpoints(this WebApplication app)
    {
        var ruleSets = app.MapGroup("/api/pricing/rule-sets");
        ruleSets.MapGet("/", async (ClaimsPrincipal user, IPricingRuleSetService service) =>
        {
            var userId = RequireUserId(user);
            await RequireAsync(user, PricingPermissionCodes.PricingRead);
            return Results.Ok(await service.GetRuleSetsAsync());
        });
        ruleSets.MapPost("/", async (CreateRuleSetRequest request, ClaimsPrincipal user, IPricingRuleSetService service) =>
        {
            var userId = RequireUserId(user);
            await RequireAsync(user, PricingPermissionCodes.PricingManage);
            return Results.Ok(await service.CreateRuleSetAsync(request.Code, request.Name, DateTime.UtcNow, userId));
        });
        ruleSets.MapPost("/{id:guid}/disable", async (Guid id, ClaimsPrincipal user, IPricingRuleSetService service) =>
        {
            var userId = RequireUserId(user);
            await RequireAsync(user, PricingPermissionCodes.PricingManage);
            await service.DisableRuleSetAsync(id, DateTime.UtcNow, userId);
            return Results.Ok(new { disabled = true });
        });
        ruleSets.MapPost("/{id:guid}/enable", async (Guid id, ClaimsPrincipal user, IPricingRuleSetService service) =>
        {
            var userId = RequireUserId(user);
            await RequireAsync(user, PricingPermissionCodes.PricingManage);
            await service.EnableRuleSetAsync(id, DateTime.UtcNow, userId);
            return Results.Ok(new { enabled = true });
        });

        // 版本生命周期
        ruleSets.MapGet("/{id:guid}/versions", async (Guid id, ClaimsPrincipal user, IPricingRuleSetService service) =>
        {
            var userId = RequireUserId(user);
            await RequireAsync(user, PricingPermissionCodes.PricingRead);
            return Results.Ok(await service.GetVersionsAsync(id));
        });
        ruleSets.MapPost("/{id:guid}/versions", async (
            Guid id, CreateVersionRequest request, ClaimsPrincipal user, IPricingRuleSetService service) =>
        {
            var userId = RequireUserId(user);
            await RequireAsync(user, PricingPermissionCodes.PricingManage);
            return Results.Ok(await service.CreateDraftVersionAsync(id, request.Definition, DateTime.UtcNow, userId));
        });
        ruleSets.MapPut("/versions/{versionId:guid}", async (
            Guid versionId, CreateVersionRequest request, ClaimsPrincipal user, IPricingRuleSetService service) =>
        {
            var userId = RequireUserId(user);
            await RequireAsync(user, PricingPermissionCodes.PricingManage);
            await service.UpdateDraftDefinitionAsync(versionId, request.Definition, DateTime.UtcNow, userId);
            return Results.Ok(new { updated = true });
        });
        ruleSets.MapGet("/versions/{versionId:guid}/validate", async (
            Guid versionId, ClaimsPrincipal user, IPricingRuleSetService service) =>
        {
            var userId = RequireUserId(user);
            await RequireAsync(user, PricingPermissionCodes.PricingRead);
            return Results.Ok(new { errors = await service.ValidateVersionAsync(versionId) });
        });
        ruleSets.MapPost("/versions/{versionId:guid}/publish", async (
            Guid versionId, ClaimsPrincipal user, IPricingRuleSetService service) =>
        {
            var userId = RequireUserId(user);
            await RequireAsync(user, PricingPermissionCodes.PricingManage);
            await service.PublishVersionAsync(versionId, DateTime.UtcNow, userId);
            return Results.Ok(new { published = true });
        });
        ruleSets.MapGet("/{id:guid}/current", async (Guid id, ClaimsPrincipal user, IPricingRuleSetService service) =>
        {
            var userId = RequireUserId(user);
            await RequireAsync(user, PricingPermissionCodes.PricingRead);
            return Results.Ok(await service.GetCurrentPublishedVersionAsync(id));
        });
    }

    /// <summary>计价试算（开单页实时试算复用）。</summary>
    public static void MapPricingTrialEndpoint(this WebApplication app)
    {
        app.MapPost("/api/pricing/trial", async (
            TrialRequest request, ClaimsPrincipal user, IPricingTrialService service) =>
        {
            RequireUserId(user);
            await RequireAsync(user, PricingPermissionCodes.PricingRead);
            return Results.Ok(await service.TrialCalculateAsync(request.RuleSetId, request.Input));
        });
    }

    public sealed record CreateRuleSetRequest(string Code, string Name);

    public sealed record CreateVersionRequest(PricingRuleDefinition Definition);

    public sealed record TrialRequest(Guid RuleSetId, PricingQuoteInput Input);

    private static async Task RequireAsync(ClaimsPrincipal user, string permissionCode)
    {
        var http = new HttpContextAccessor().HttpContext
            ?? throw new AuthenticationFailedException("无效的请求上下文");
        var authorization = http.RequestServices.GetRequiredService<IAuthorizationService>();
        var userId = RequireUserId(user);
        if (!await authorization.HasPermissionAsync(userId, permissionCode))
        {
            throw new PermissionDeniedException(permissionCode);
        }
    }

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new AuthenticationFailedException("无效的认证凭据");
}
