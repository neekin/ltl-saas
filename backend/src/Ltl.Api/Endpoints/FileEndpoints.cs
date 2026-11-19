using System.Security.Claims;
using Ltl.Modules.Organization.Application.Auth;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Platform.Application;
using Ltl.SharedKernel.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ltl.Api.Endpoints;

/// <summary>文件端点（FILE-001，认证 + RBAC 实时权限校验；multipart 上传 + 元数据 + 内容下载）。</summary>
public static class FileEndpoints
{
    public static void MapFileEndpoints(this WebApplication app)
    {
        var files = app.MapGroup("/api/files");

        files.MapPost("/", async (
            IFormFile file, ClaimsPrincipal user, IAuthorizationService authorization, IFileObjectService service) =>
        {
            var userId = RequireUserId(user);
            await RequireFileAccessAsync(authorization, userId);
            return Results.Ok(await service.UploadAsync(
                userId, file.FileName, file.ContentType, file.OpenReadStream(), file.Length));
        });

        files.MapGet("/{id:guid}", async (
            Guid id, ClaimsPrincipal user, IAuthorizationService authorization, IFileObjectService service) =>
        {
            await RequireFileAccessAsync(authorization, RequireUserId(user));
            return Results.Ok(await service.GetAsync(id));
        });

        files.MapGet("/{id:guid}/content", async (
            Guid id, ClaimsPrincipal user, IAuthorizationService authorization, IFileObjectService service) =>
        {
            await RequireFileAccessAsync(authorization, RequireUserId(user));
            var (metadata, content) = await service.GetContentAsync(id);
            return Results.Stream(content, metadata.ContentType, metadata.FileName);
        });
    }

    /// <summary>校验操作人为有效已授权业务用户（实时查库；禁用用户或无任何角色权限的账号拒绝访问）。</summary>
    private static async Task RequireFileAccessAsync(IAuthorizationService authorization, Guid userId)
    {
        var permissions = await authorization.GetPermissionCodesAsync(userId);
        if (permissions.Count == 0)
        {
            throw new PermissionDeniedException("file.access");
        }
    }

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new AuthenticationFailedException("无效的认证凭据");
}
