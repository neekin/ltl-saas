namespace Ltl.Modules.Waybills.Domain;

/// <summary>运单状态（waybills.waybill_status）。流转仅经领域方法守卫（WB-001 设计文档第 3 节）。</summary>
public enum WaybillStatus
{
    /// <summary>草稿（开单后未确认；货物件/资料可调整）。</summary>
    Draft = 1,

    /// <summary>已确认（进入后续收货/运输流程；不可编辑）。</summary>
    Confirmed = 2,

    /// <summary>已取消（终态，不可恢复）。</summary>
    Cancelled = 3,
}
