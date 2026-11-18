namespace Ltl.Modules.Platform.Domain;

/// <summary>通知通道（白名单）。sms/wechat 需配置服务商凭证；未配置时以 log 通道落记录。</summary>
public enum NotificationChannel
{
    /// <summary>日志通道（开发/未配置服务商时的兜底）。</summary>
    Log = 1,

    /// <summary>短信（预留服务商适配）。</summary>
    Sms = 2,

    /// <summary>微信（订阅消息/公众号模板消息，预留服务商适配）。</summary>
    WeChat = 3,
}

/// <summary>通知投递状态。</summary>
public enum NotificationStatus
{
    /// <summary>待发送（服务商未配置时短暂停留）。</summary>
    Pending = 1,

    /// <summary>已发送（含 log 通道成功落日志）。</summary>
    Sent = 2,

    /// <summary>发送失败（服务商返回错误；保留重试依据）。</summary>
    Failed = 3,
}
