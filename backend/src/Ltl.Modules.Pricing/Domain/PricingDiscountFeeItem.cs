using System.Text.Json.Serialization;

namespace Ltl.Modules.Pricing.Domain;

/// <summary>折扣方式（白名单）。</summary>
public enum PricingDiscountMode
{
    /// <summary>按比例折扣（Rate%，不含全免；全免用 Amount）。</summary>
    Rate = 1,

    /// <summary>固定减免金额。</summary>
    Amount = 2,
}

/// <summary>
/// 折扣项（每条规则至多 1 个，结构保证；作用对象 = 运费主项 + 附加费合计）。
/// </summary>
public sealed class PricingDiscountFeeItem
{
    /// <summary>费用项名称（≤100）。</summary>
    public required string Name { get; init; }

    public required PricingDiscountMode Mode { get; init; }

    /// <summary>仅 Rate：折扣率（0,100）。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Rate { get; init; }

    /// <summary>仅 Amount：固定减免金额 ≥0。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Amount { get; init; }
}
