namespace Ltl.Modules.Waybills.Domain;

/// <summary>
/// COD 代收货款状态（waybills.cod_status），独立于运费财务状态（AGENTS §6.5：COD 不是运费收入）。
/// 流转由 P8 COD 任务维护；本任务仅落初始值（无代收 → None，有代收 → PendingCollection）。
/// </summary>
public enum WaybillCodStatus
{
    /// <summary>无代收货款。</summary>
    None = 1,

    /// <summary>待收取（创建时 COD 金额 >0）。</summary>
    PendingCollection = 2,

    /// <summary>已收取待结算（P8）。</summary>
    Collected = 3,

    /// <summary>已结算（P8）。</summary>
    Settled = 4,

    /// <summary>已付款给发货人（P8）。</summary>
    PaidOut = 5,
}
