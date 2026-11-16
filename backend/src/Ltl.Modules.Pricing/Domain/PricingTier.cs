using System.Text.Json.Serialization;

namespace Ltl.Modules.Pricing.Domain;

/// <summary>
/// 阶梯计费档位。区间语义：[上一档 UpTo, 本档 UpTo) 内的计费数量按 UnitPrice 计费；
/// 末档 UpTo = null 表示无上限（"超出部分计费"）。
/// </summary>
public sealed class PricingTier
{
    /// <summary>本档上界（不含）；null = 无上限，仅允许末档。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? UpTo { get; init; }

    /// <summary>本档单价（每计费单位，元），必须 >0。</summary>
    public required decimal UnitPrice { get; init; }
}
