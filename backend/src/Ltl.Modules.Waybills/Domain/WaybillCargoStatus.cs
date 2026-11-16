namespace Ltl.Modules.Waybills.Domain;

/// <summary>
/// 货物/仓储状态（waybills.cargo_status），与运单状态分离（AGENTS §6.1）。
/// 流转由 P4 收货/入库/分拣任务实现，本任务仅落初始值。
/// </summary>
public enum WaybillCargoStatus
{
    /// <summary>待收货（初始）。</summary>
    PendingReceive = 1,

    /// <summary>已收货入库。</summary>
    Received = 2,

    /// <summary>已分拣。</summary>
    Sorted = 3,
}
