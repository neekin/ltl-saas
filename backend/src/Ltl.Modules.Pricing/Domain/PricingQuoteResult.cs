namespace Ltl.Modules.Pricing.Domain;

/// <summary>费用项类别。</summary>
public enum PricingFeeKind
{
    /// <summary>运费主项。</summary>
    Freight = 1,

    /// <summary>附加费。</summary>
    Surcharge = 2,
}

/// <summary>
/// 单项费用（含人类可读计算过程，ROADMAP P3"计价结果能解释每项费用的来源"）。
/// </summary>
public sealed class PricingQuoteItem
{
    public required PricingFeeKind Kind { get; init; }

    /// <summary>费用项名称（来自规则定义）。</summary>
    public required string Name { get; init; }

    /// <summary>金额（已按舍入策略保留 2 位）。</summary>
    public required decimal Amount { get; init; }

    /// <summary>计算过程解释（如"计费重量 3.35 kg：[0,1)×18 + [1,∞) 2.35×4.2"）。</summary>
    public required string Detail { get; init; }
}

/// <summary>折扣明细。</summary>
public sealed class PricingQuoteDiscount
{
    public required string Name { get; init; }

    /// <summary>折扣金额（正数，已舍入）。</summary>
    public required decimal Amount { get; init; }

    public required string Detail { get; init; }
}

/// <summary>
/// 计价结果（纯函数产物，同输入同定义恒等）。小计/总额为已舍入项的代数和。
/// </summary>
public sealed class PricingQuoteResult
{
    public required int MatchedRulePriority { get; init; }

    public required string MatchedRuleName { get; init; }

    /// <summary>计费数量（已舍入；Fixed 运费时为 null）。</summary>
    public decimal? BillingQuantity { get; init; }

    /// <summary>计费口径解释（如"计费重量 3.35 kg（取大：重量 3.00 vs 体积重 3.33）"）。</summary>
    public string? BillingBasis { get; init; }

    public required IReadOnlyList<PricingQuoteItem> Items { get; init; }

    /// <summary>保价费合计（规则 PerDeclaredValue 附加费；未命中保价规则或未声明价值时为 0/null）。</summary>
    public decimal? InsuranceFee { get; init; }

    public PricingQuoteDiscount? Discount { get; init; }

    /// <summary>运费 + 附加费合计。</summary>
    public required decimal Subtotal { get; init; }

    /// <summary>最终金额 = max(小计 − 折扣, 0)。</summary>
    public required decimal Total { get; init; }
}
