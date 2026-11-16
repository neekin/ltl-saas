namespace Ltl.Modules.Pricing.Domain;

/// <summary>
/// 规则集版本定义快照（pricing_rule_set_versions.definition，JSONB）。
/// 权威结构 = 本 C# 模型；序列化选项见 Infrastructure.PricingJson（未知成员/枚举/类型拒绝）。
/// 已发布版本的本快照不可变（ADR-0007）。
/// </summary>
public sealed class PricingRuleDefinition
{
    /// <summary>金额舍入策略（v1 仅 HalfUpTwoDecimals）。</summary>
    public required RoundingPolicy Rounding { get; init; }

    /// <summary>规则列表（≥1 条）。</summary>
    public required IReadOnlyList<PricingRule> Rules { get; init; }
}
