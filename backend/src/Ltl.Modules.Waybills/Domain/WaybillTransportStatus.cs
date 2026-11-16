namespace Ltl.Modules.Waybills.Domain;

/// <summary>
/// 运输状态（waybills.transport_status），与运单状态分离（AGENTS §6.1）。
/// 流转由 P5 配载/发车/区段任务实现，本任务仅落初始值；取消运单以"未发运"为前提。
/// </summary>
public enum WaybillTransportStatus
{
    /// <summary>未发运（初始）。</summary>
    NotShipped = 1,

    /// <summary>已发运（批次发车后；TRIP-002）。解锁"已发运不可取消"守卫（WB-001 §3）。</summary>
    Shipped = 2,
}
