using Ltl.Modules.Pricing.Domain;

namespace Ltl.Modules.Pricing.Application;

/// <summary>
/// 计价试算服务（PRICE-002）：对规则集**当前已发布版本**执行匹配与计算。
/// 无生效版本/规则集停用/无命中规则均明确报错（不静默计零）。
/// HTTP 端点随登录任务交付；WB-001/002 开单计价复用本服务并固化结果快照。
/// </summary>
public interface IPricingTrialService
{
    /// <summary>按当前已发布版本试算。结果含版本引用与输入快照，可复现。</summary>
    /// <exception cref="EntityNotFoundException">规则集不存在或不可访问（跨租户）。</exception>
    /// <exception cref="PricingRuleViolationException">规则集停用或无已发布版本。</exception>
    /// <exception cref="PricingMatchFailedException">版本内无命中规则。</exception>
    Task<PricingTrialResult> TrialCalculateAsync(Guid ruleSetId, PricingQuoteInput input);
}
