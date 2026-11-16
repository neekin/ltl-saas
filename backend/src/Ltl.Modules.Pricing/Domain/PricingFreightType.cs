namespace Ltl.Modules.Pricing.Domain;

/// <summary>运费主项计费类型（白名单）。每条规则恰好一个运费项。</summary>
public enum PricingFreightType
{
    /// <summary>固定运费（整票一口价）。</summary>
    Fixed = 1,

    /// <summary>按计费重量单价计费。</summary>
    ByWeight = 2,

    /// <summary>按体积单价计费。</summary>
    ByVolume = 3,

    /// <summary>按件数单价计费。</summary>
    ByPieces = 4,

    /// <summary>阶梯计费（分段单价；末档无上限 = 超出部分计费）。</summary>
    Tiered = 5,
}
