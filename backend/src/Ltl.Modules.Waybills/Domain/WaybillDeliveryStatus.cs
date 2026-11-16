namespace Ltl.Modules.Waybills.Domain;

/// <summary>
/// 交付/配送状态（waybills.delivery_status），与运单状态、财务状态分离（AGENTS §6.1：签收 ≠ 运费结清）。
/// 流转由 P6 到货/交付/签收任务实现，本任务仅落初始值。
/// </summary>
public enum WaybillDeliveryStatus
{
    /// <summary>待交付（初始）。</summary>
    NotDelivered = 1,
}
