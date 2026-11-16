using System.Text.Json.Serialization;

namespace Ltl.Modules.Pricing.Domain;

/// <summary>
/// 单条计价规则（版本定义内）。匹配语义（PRICE-002 契约）：
/// 按优先级升序取第一个命中的启用规则；每规则恰好一个运费项 + 0..n 附加费 + 至多 1 折扣。
/// </summary>
public sealed class PricingRule
{
    /// <summary>优先级（≥1，集合内唯一；升序 = 匹配顺序）。</summary>
    public required int Priority { get; init; }

    /// <summary>规则名称（用于计价解释，≤200）。</summary>
    public required string Name { get; init; }

    /// <summary>是否启用（未启用规则参与校验但不参与匹配）。</summary>
    public required bool Enabled { get; init; }

    /// <summary>计费重量策略。</summary>
    public required BillingWeightStrategy BillingWeightStrategy { get; init; }

    /// <summary>体积重系数（m³ → kg）；Max/Min 策略必填 >0。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? VolumeToWeightFactor { get; init; }

    /// <summary>匹配条件（全部 AND；空 = 无条件默认规则）。</summary>
    public required IReadOnlyList<PricingCondition> Conditions { get; init; }

    /// <summary>运费主项（恰好一个，结构保证）。</summary>
    public required PricingFreightFeeItem Freight { get; init; }

    /// <summary>附加费项（0..n）。</summary>
    public required IReadOnlyList<PricingSurchargeFeeItem> Surcharges { get; init; }

    /// <summary>折扣项（0..1）。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PricingDiscountFeeItem? Discount { get; init; }
}
