namespace Ltl.Modules.Pricing.Domain;

/// <summary>规则集状态（pricing_rule_sets.status）。停用规则集即停止其全部版本的匹配。</summary>
public enum PricingRuleSetStatus
{
    /// <summary>启用（可发布版本、参与匹配）。</summary>
    Active = 1,

    /// <summary>停用（不参与匹配；历史版本保留）。</summary>
    Disabled = 2,
}
