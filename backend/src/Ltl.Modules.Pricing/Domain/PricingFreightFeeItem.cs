using System.Text.Json.Serialization;

namespace Ltl.Modules.Pricing.Domain;

/// <summary>
/// 运费主项（每条规则恰好一个，结构保证）。金额必须 >0（AGENTS §6.4：不得静默按零元计费；
/// 免运费促销用 Discount 表达）。
/// </summary>
public sealed class PricingFreightFeeItem
{
    /// <summary>费用项名称（用于计价解释，≤100）。</summary>
    public required string Name { get; init; }

    public required PricingFreightType Type { get; init; }

    /// <summary>仅 Fixed：整票固定金额，>0。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Amount { get; init; }

    /// <summary>仅 ByWeight/ByVolume/ByPieces：每计费单位单价，>0。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? UnitPrice { get; init; }

    /// <summary>仅 ByWeight/ByVolume/ByPieces/Tiered：最低收费/起步价（下限），≥0 可选。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? MinCharge { get; init; }

    /// <summary>仅 Tiered：阶梯档位（≥1，严格递增，末档无上限）。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<PricingTier>? Tiers { get; init; }
}
