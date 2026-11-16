namespace Ltl.Modules.Pricing.Domain;

/// <summary>
/// 规则集版本状态（pricing_rule_set_versions.status）。
/// Draft → 发布校验零错误 → Published（不可变）→ 新版本发布后 → Superseded（历史保留）。
/// </summary>
public enum PricingVersionStatus
{
    /// <summary>草稿（可编辑定义）。</summary>
    Draft = 0,

    /// <summary>已发布（不可变快照；每规则集同时最多一个，部分唯一索引强制）。</summary>
    Published = 1,

    /// <summary>已被新版本取代（历史保留，永不删除）。</summary>
    Superseded = 2,
}
