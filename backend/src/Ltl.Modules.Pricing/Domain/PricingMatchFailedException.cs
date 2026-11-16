namespace Ltl.Modules.Pricing.Domain;

/// <summary>
/// 规则匹配失败（无命中规则）。AGENTS §6.4：规则匹配失败必须明确报错，不得静默按零元计费。
/// 后续 API 任务映射为明确业务错误（422 类）。
/// </summary>
public sealed class PricingMatchFailedException(string message) : Exception(message);
