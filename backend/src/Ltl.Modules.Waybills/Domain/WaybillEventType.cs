namespace Ltl.Modules.Waybills.Domain;

/// <summary>
/// 运单事件类型（waybill_events.event_type）。AGENTS §6.1：关键状态变化必须记录事件。
/// 后续任务（收货/发车/签收等）追加枚举值。
/// </summary>
public enum WaybillEventType
{
    /// <summary>开单（→ Draft）。</summary>
    Created = 1,

    /// <summary>确认（Draft → Confirmed）。</summary>
    Confirmed = 2,

    /// <summary>取消（→ Cancelled，终态）。</summary>
    Cancelled = 3,
}
