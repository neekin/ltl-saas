namespace Ltl.Modules.Pricing.Domain;

/// <summary>
/// 计价规则领域违规（生命周期守卫、定义校验失败等）。后续 API 任务映射为 409/422 类错误。
/// </summary>
public sealed class PricingRuleViolationException(string message) : Exception(message);
