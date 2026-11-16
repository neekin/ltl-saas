namespace Ltl.Modules.Pricing.Domain;

/// <summary>
/// 规则条件操作符白名单（AGENTS §6.4）。数值字段允许区间/比较操作符；Id 字段仅允许等值/包含。
/// </summary>
public enum PricingConditionOperator
{
    /// <summary>等于（单值）。</summary>
    Equals = 1,

    /// <summary>包含（多值之一）。</summary>
    In = 2,

    /// <summary>大于。</summary>
    GreaterThan = 3,

    /// <summary>大于等于。</summary>
    GreaterOrEqual = 4,

    /// <summary>小于。</summary>
    LessThan = 5,

    /// <summary>小于等于。</summary>
    LessOrEqual = 6,

    /// <summary>闭区间 [Value, Value2]。</summary>
    Between = 7,
}
