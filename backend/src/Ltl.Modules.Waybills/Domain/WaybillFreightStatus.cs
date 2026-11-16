namespace Ltl.Modules.Waybills.Domain;

/// <summary>
/// 运费财务状态（waybills.freight_status），与运单状态、交付状态分离（AGENTS §6.1）。
/// 流转由 P7 收款核销任务维护；本任务仅落初始值 Unpaid。
/// </summary>
public enum WaybillFreightStatus
{
    /// <summary>未收款（初始）。</summary>
    Unpaid = 1,

    /// <summary>部分收款（P7）。</summary>
    PartiallyPaid = 2,

    /// <summary>已结清（P7）。</summary>
    Settled = 3,
}
