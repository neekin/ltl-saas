using System.Security.Claims;
using Ltl.Modules.Organization.Application.Auth;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Pricing.Application;
using Ltl.Modules.Pricing.Domain;
using Ltl.SharedKernel.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ltl.Api.Endpoints;

/// <summary>
/// 计价管理端点（PRICE-003）：规则集/版本生命周期 + 试算。
/// 权限在端点层实时校验（pricing.read/manage）——计价服务同时被开单命令复用（服务层不重复校验）。
/// 响应统一映射为 DTO，不直接暴露 EF Core 实体（AGENTS §8）。
/// </summary>
public static class PricingEndpoints
{
    public static void MapPricingEndpoints(this WebApplication app)
    {
        var ruleSets = app.MapGroup("/api/pricing/rule-sets");
        ruleSets.MapGet("/", async (
            ClaimsPrincipal user, IAuthorizationService authorization, IPricingRuleSetService service) =>
        {
            await RequireAsync(user, authorization, PricingPermissionCodes.PricingRead);
            var list = await service.GetRuleSetsAsync();
            return Results.Ok(list.Select(ToDto).ToList());
        });
        ruleSets.MapPost("/", async (
            CreateRuleSetRequest request, ClaimsPrincipal user, IAuthorizationService authorization, IPricingRuleSetService service) =>
        {
            var userId = await RequireAsync(user, authorization, PricingPermissionCodes.PricingManage);
            var created = await service.CreateRuleSetAsync(request.Code, request.Name, DateTime.UtcNow, userId);
            return Results.Ok(ToDto(created));
        });
        ruleSets.MapPost("/{id:guid}/disable", async (
            Guid id, ClaimsPrincipal user, IAuthorizationService authorization, IPricingRuleSetService service) =>
        {
            var userId = await RequireAsync(user, authorization, PricingPermissionCodes.PricingManage);
            await service.DisableRuleSetAsync(id, DateTime.UtcNow, userId);
            return Results.Ok(new { disabled = true });
        });
        ruleSets.MapPost("/{id:guid}/enable", async (
            Guid id, ClaimsPrincipal user, IAuthorizationService authorization, IPricingRuleSetService service) =>
        {
            var userId = await RequireAsync(user, authorization, PricingPermissionCodes.PricingManage);
            await service.EnableRuleSetAsync(id, DateTime.UtcNow, userId);
            return Results.Ok(new { enabled = true });
        });

        // 版本生命周期
        ruleSets.MapGet("/{id:guid}/versions", async (
            Guid id, ClaimsPrincipal user, IAuthorizationService authorization, IPricingRuleSetService service) =>
        {
            await RequireAsync(user, authorization, PricingPermissionCodes.PricingRead);
            var versions = await service.GetVersionsAsync(id);
            return Results.Ok(versions.Select(ToDto).ToList());
        });
        ruleSets.MapPost("/{id:guid}/versions", async (
            Guid id, CreateVersionRequest request, ClaimsPrincipal user, IAuthorizationService authorization, IPricingRuleSetService service) =>
        {
            var userId = await RequireAsync(user, authorization, PricingPermissionCodes.PricingManage);
            var version = await service.CreateDraftVersionAsync(id, request.Definition, DateTime.UtcNow, userId);
            return Results.Ok(ToDto(version));
        });
        ruleSets.MapPut("/versions/{versionId:guid}", async (
            Guid versionId, CreateVersionRequest request, ClaimsPrincipal user, IAuthorizationService authorization, IPricingRuleSetService service) =>
        {
            var userId = await RequireAsync(user, authorization, PricingPermissionCodes.PricingManage);
            await service.UpdateDraftDefinitionAsync(versionId, request.Definition, DateTime.UtcNow, userId);
            return Results.Ok(new { updated = true });
        });
        ruleSets.MapGet("/versions/{versionId:guid}/validate", async (
            Guid versionId, ClaimsPrincipal user, IAuthorizationService authorization, IPricingRuleSetService service) =>
        {
            await RequireAsync(user, authorization, PricingPermissionCodes.PricingRead);
            return Results.Ok(new { errors = await service.ValidateVersionAsync(versionId) });
        });
        ruleSets.MapPost("/versions/{versionId:guid}/publish", async (
            Guid versionId, ClaimsPrincipal user, IAuthorizationService authorization, IPricingRuleSetService service) =>
        {
            var userId = await RequireAsync(user, authorization, PricingPermissionCodes.PricingManage);
            await service.PublishVersionAsync(versionId, DateTime.UtcNow, userId);
            return Results.Ok(new { published = true });
        });
        ruleSets.MapGet("/{id:guid}/current", async (
            Guid id, ClaimsPrincipal user, IAuthorizationService authorization, IPricingRuleSetService service) =>
        {
            await RequireAsync(user, authorization, PricingPermissionCodes.PricingRead);
            var current = await service.GetCurrentPublishedVersionAsync(id);
            return current is null ? Results.NotFound() : Results.Ok(ToDto(current));
        });
    }

    /// <summary>计价试算（开单页实时试算复用）。</summary>
    public static void MapPricingTrialEndpoint(this WebApplication app)
    {
        app.MapPost("/api/pricing/trial", async (
            TrialRequest request, ClaimsPrincipal user, IAuthorizationService authorization, IPricingTrialService service) =>
        {
            await RequireAsync(user, authorization, PricingPermissionCodes.PricingRead);
            return Results.Ok(await service.TrialCalculateAsync(request.RuleSetId, request.Input));
        });
    }

    public sealed record CreateRuleSetRequest(string Code, string Name);

    public sealed record CreateVersionRequest(PricingRuleDefinition Definition);

    public sealed record TrialRequest(Guid RuleSetId, PricingQuoteInput Input);

    public sealed record PricingRuleSetDto(
        Guid Id, string Code, string Name, string Status, DateTime CreatedAt, DateTime? UpdatedAt);

    public sealed record PricingRuleSetVersionDto(
        Guid Id, Guid RuleSetId, int VersionNumber, string Status,
        PricingRuleDefinition Definition, DateTime? PublishedAt, DateTime CreatedAt);

    private static PricingRuleSetDto ToDto(PricingRuleSet entity) => new(
        entity.Id, entity.Code, entity.Name, entity.Status.ToString(), entity.CreatedAt, entity.UpdatedAt);

    private static PricingRuleSetVersionDto ToDto(PricingRuleSetVersion entity) => new(
        entity.Id, entity.RuleSetId, entity.VersionNumber, entity.Status.ToString(),
        entity.Definition, entity.PublishedAt, entity.CreatedAt);

    private static async Task<Guid> RequireAsync(
        ClaimsPrincipal user, IAuthorizationService authorization, string permissionCode)
    {
        var userId = RequireUserId(user);
        if (!await authorization.HasPermissionAsync(userId, permissionCode))
        {
            throw new PermissionDeniedException(permissionCode);
        }

        return userId;
    }

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new AuthenticationFailedException("无效的认证凭据");
}
