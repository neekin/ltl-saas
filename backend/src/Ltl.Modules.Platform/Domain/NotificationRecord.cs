namespace Ltl.Modules.Platform.Domain;

/// <summary>
/// 通知记录（追加式，NOTIFY-001）。运单关键事件（收货/发车/到达/签收）自动生成；
/// 短信/微信真实外发由 INotificationSender 服务商适配层完成，未配置凭证时走 log 通道。
/// 记录不可修改删除（审计口径，同 AGENTS §6.5）。
/// </summary>
public sealed class NotificationRecord : TenantScopedEntity
{
    public NotificationChannel Channel { get; private set; }

    /// <summary>业务事件类型（waybill.received / waybill.departed / waybill.arrived / waybill.delivered）。</summary>
    public string EventType { get; private set; } = null!;

    /// <summary>关联运单（可空：预留非运单类通知）。</summary>
    public Guid? WaybillId { get; private set; }

    /// <summary>接收人（手机号/微信 OpenID 占位；log 通道记录目标）。</summary>
    public string Recipient { get; private set; } = null!;

    /// <summary>通知内容。</summary>
    public string Content { get; private set; } = null!;

    public NotificationStatus Status { get; private set; }

    /// <summary>发送失败原因（Status = Failed 时记录）。</summary>
    public string? FailureReason { get; private set; }

    public DateTime? SentAt { get; private set; }

    private NotificationRecord()
    {
    }

    /// <exception cref="ArgumentException">事件类型/接收人/内容非法时抛出。</exception>
    public static NotificationRecord Create(
        Guid tenantId, NotificationChannel channel, string eventType,
        Guid? waybillId, string recipient, string content,
        DateTime nowUtc, Guid? createdBy = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("租户 Id 不能为空 Guid。", nameof(tenantId));
        }
        if (string.IsNullOrWhiteSpace(eventType) || eventType.Trim().Length > 64)
        {
            throw new ArgumentException("事件类型不能为空且长度 ≤64。", nameof(eventType));
        }
        if (string.IsNullOrWhiteSpace(recipient) || recipient.Trim().Length > 200)
        {
            throw new ArgumentException("接收人不能为空且长度 ≤200。", nameof(recipient));
        }
        if (string.IsNullOrWhiteSpace(content) || content.Trim().Length > 500)
        {
            throw new ArgumentException("通知内容不能为空且长度 ≤500。", nameof(content));
        }

        return new NotificationRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Channel = channel,
            EventType = eventType.Trim(),
            WaybillId = waybillId,
            Recipient = recipient.Trim(),
            Content = content.Trim(),
            Status = NotificationStatus.Pending,
            CreatedAt = EnsureUtc(nowUtc),
            CreatedBy = createdBy,
        };
    }

    /// <summary>标记已发送。</summary>
    public void MarkSent(DateTime nowUtc)
    {
        if (Status == NotificationStatus.Sent)
        {
            return;
        }

        Status = NotificationStatus.Sent;
        SentAt = EnsureUtc(nowUtc);
    }

    /// <summary>标记发送失败（保留原因）。</summary>
    public void MarkFailed(string reason, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("失败原因不能为空。", nameof(reason));
        }

        Status = NotificationStatus.Failed;
        FailureReason = reason.Trim();
        SentAt = EnsureUtc(nowUtc);
    }
}
