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

    /// <summary>收货登记（货物/仓储状态：待收货 → 已收货，RECEIVE-001）。</summary>
    Received = 4,

    /// <summary>分拣完成（货物/仓储状态：已收货 → 已分拣，WAREHOUSE-001）。</summary>
    Sorted = 5,

    /// <summary>批次发车（运输状态：未发运 → 已发运，TRIP-002）。</summary>
    Departed = 6,

    /// <summary>批次到达（TRIP-002；不改交付状态）。</summary>
    Arrived = 7,
}
