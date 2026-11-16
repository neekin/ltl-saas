using Ltl.Modules.Waybills.Domain;

namespace Ltl.Modules.Waybills.Application;

/// <summary>
/// 运单命令服务（WB-002）：开单（计价 + 快照 + 事件 + 幂等）、确认、取消、事件时间线。
/// 无 HTTP 端点（认证交付后随登录任务暴露）。
/// </summary>
public interface IWaybillService
{
    /// <summary>
    /// 开单：跨模块资料校验（原生 SQL 租户校验）→ 计价试算（失败明确报错、不消耗单号）→
    /// 取运单号 → 创建草稿并固化费用 → 计价快照 + 开单事件（同事务）。幂等键命中返回已存在运单。
    /// </summary>
    /// <exception cref="EntityNotFoundException">网点/客户/线路不存在或不可访问（跨租户）。</exception>
    /// <exception cref="WaybillDomainException">网点/客户已停用。</exception>
    /// <exception cref="PricingRuleViolationException">规则集停用或无已发布版本。</exception>
    /// <exception cref="PricingMatchFailedException">无命中计价规则。</exception>
    Task<CreateWaybillResult> CreateWaybillAsync(CreateWaybillCommand command);

    /// <summary>确认运单（仅草稿；记录事件，同事务）。</summary>
    Task<Waybill> ConfirmWaybillAsync(Guid waybillId, Guid? branchId = null, Guid? operatorId = null);

    /// <summary>取消运单（未发运的草稿/已确认；终态；记录事件，同事务）。</summary>
    Task<Waybill> CancelWaybillAsync(
        Guid waybillId, string? reason = null, Guid? branchId = null, Guid? operatorId = null);

    /// <summary>运单事件时间线（按发生时间正序）。</summary>
    Task<IReadOnlyList<WaybillEvent>> GetEventsAsync(Guid waybillId);
}
