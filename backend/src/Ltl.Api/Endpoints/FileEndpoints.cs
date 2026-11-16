using System.Security.Claims;
using Ltl.Modules.Platform.Application;
using Ltl.Modules.Organization.Application.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ltl.Api.Endpoints;

/// <summary>文件端点（FILE-001，认证；multipart 上传 + 元数据 + 内容下载）。</summary>
public static class FileEndpoints
{
    public static void MapFileEndpoints(this WebApplication app)
    {
        var files = app.MapGroup("/api/files");

        files.MapPost("/", async (IFormFile file, ClaimsPrincipal user, IFileObjectService service) =>
            Results.Ok(await service.UploadAsync(
                RequireUserId(user), file.FileName, file.ContentType, file.OpenReadStream(), file.Length)));

        files.MapGet("/{id:guid}", async (Guid id, IFileObjectService service) =>
            Results.Ok(await service.GetAsync(id)));

        files.MapGet("/{id:guid}/content", async (Guid id, IFileObjectService service) =>
        {
            var (metadata, content) = await service.GetContentAsync(id);
            return Results.Stream(content, metadata.ContentType, metadata.FileName);
        });
    }

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new AuthenticationFailedException("无效的认证凭据");
}
