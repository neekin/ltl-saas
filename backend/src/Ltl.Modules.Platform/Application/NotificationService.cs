using Ltl.Modules.Platform.Domain;
using Ltl.Modules.Platform.Infrastructure;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ltl.Modules.Platform.Application;

public sealed record NotificationDto(
    Guid Id, string Channel, string EventType, Guid? WaybillId,
    string Recipient, string Content, string Status, string? FailureReason, DateTime? SentAt, DateTime CreatedAt);

/// <summary>
/// 通知服务（NOTIFY-001）。运单关键事件调用 EnqueueAsync 生成通知记录；
/// 发送走 INotificationSender 服务商抽象：当前实现 LogNotificationSender（log 通道），
/// 短信/微信服务商接入时实现对应 Sender 并在 DI 按配置选择。
/// </summary>
public interface INotificationService
{
    /// <summary>生成通知记录并尝试投递（同步落库；失败记录 Failed 便于重查）。</summary>
    Task EnqueueAsync(NotificationEnvelope envelope);

    /// <summary>通知记录列表（分页，新→旧）。权限：notification.read。</summary>
    Task<(IReadOnlyList<NotificationDto> Items, int Total)> ListAsync(
        Guid operatorUserId, int page, int pageSize, string? eventType);
}

/// <param name="EventType">业务事件类型（waybill.received / departed / arrived / delivered）。</param>
/// <param name="WaybillId">关联运单（可空）。</param>
/// <param name="Recipient">接收人（手机号 / OpenID 占位）。</param>
/// <param name="Content">通知内容（≤500）。</param>
public sealed record NotificationEnvelope(
    string EventType, Guid? WaybillId, string Recipient, string Content, Guid? OperatorUserId = null);

/// <summary>
/// 通知读取授权回调（依赖倒置：Platform 不反向依赖 Organization 的授权实现，
/// 由 API 组合根注入适配；保持模块依赖方向，AGENTS §4.2）。
/// </summary>
public interface INotificationAuthorizer
{
    Task<bool> HasPermissionAsync(Guid operatorUserId, string permissionCode);
}

public interface INotificationSender
{
    NotificationChannel Channel { get; }

    /// <summary>投递一条通知；抛异常视为失败（记录 Failed + 原因）。</summary>
    Task SendAsync(string recipient, string content);
}

/// <summary>
/// 日志通道发送器（默认实现）：内容写结构化日志，状态置 Sent。
/// 短信/微信服务商接入时新增实现（如 AliyunSmsSender/WeChatSender）并按配置选择。
/// </summary>
public sealed class LogNotificationSender(ILogger<LogNotificationSender> logger) : INotificationSender
{
    public NotificationChannel Channel => NotificationChannel.Log;

    public Task SendAsync(string recipient, string content)
    {
        logger.LogInformation("[通知] 收件人 {Recipient}：{Content}", recipient, content);
        return Task.CompletedTask;
    }
}

public sealed class NotificationService(
    PlatformDbContext db,
    INotificationSender sender,
    INotificationAuthorizer authorizer) : INotificationService
{
    public async Task EnqueueAsync(NotificationEnvelope envelope)
    {
        if (db.CurrentTenantId is not { } tenantId)
        {
            // 通知挂接在租户业务流程内，缺失租户上下文属编程错误
            throw new TenantContextMissingException();
        }

        var record = NotificationRecord.Create(
            tenantId, sender.Channel, envelope.EventType, envelope.WaybillId,
            envelope.Recipient, envelope.Content, DateTime.UtcNow, envelope.OperatorUserId);

        try
        {
            await sender.SendAsync(record.Recipient, record.Content);
            record.MarkSent(DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            record.MarkFailed(ex.Message, DateTime.UtcNow);
        }

        db.NotificationRecords.Add(record);
        await db.SaveChangesAsync();
    }

    public async Task<(IReadOnlyList<NotificationDto> Items, int Total)> ListAsync(
        Guid operatorUserId, int page, int pageSize, string? eventType)
    {
        if (!await authorizer.HasPermissionAsync(operatorUserId, "notification.read"))
        {
            throw new PlatformPermissionDeniedException("notification.read");
        }

        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = db.NotificationRecords.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(n => n.EventType == eventType.Trim());
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return (items.Select(ToDto).ToList(), total);
    }

    private static NotificationDto ToDto(NotificationRecord n) => new(
        n.Id, n.Channel.ToString(), n.EventType, n.WaybillId,
        n.Recipient, n.Content, n.Status.ToString(), n.FailureReason, n.SentAt, n.CreatedAt);
}
