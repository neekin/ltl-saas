using System.Security.Claims;
using Ltl.Modules.Organization.Application.Auth;
using Ltl.Modules.Platform.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ltl.Api.Endpoints;

/// <summary>通知记录端点（NOTIFY-001）。通知由运单事件自动生成，此处仅查询。</summary>
public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var notifications = app.MapGroup("/api/notifications");
        notifications.MapGet("/", async (
            int? page, int? pageSize, string? eventType,
            ClaimsPrincipal user, INotificationService service) =>
            // 返回具名 record（Items/Total 为公开属性），System.Text.Json 默认 camelCase 直接序列化为
            // { items, total }。旧版返回 C# tuple 会序列化为空对象，故曾改用匿名投影规避（commit 2ae2d39）。
            Results.Ok(await service.ListAsync(RequireUserId(user), page ?? 1, pageSize ?? 20, eventType)));
    }

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new AuthenticationFailedException("无效的认证凭据");
}
