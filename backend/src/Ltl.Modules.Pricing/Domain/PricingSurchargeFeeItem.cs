namespace Ltl.Modules.Pricing.Domain;

/// <summary>
/// 附加费项（提货、送货、装卸、包装等，0..n 个）。Amount ≥0。
/// </summary>
public sealed class PricingSurchargeFeeItem
{
    /// <summary>费用项名称（≤100）。</summary>
    public required string Name { get; init; }

    public required PricingSurchargeBasis Basis { get; init; }

    /// <summary>金额（按 Basis 的单位），≥0。</summary>
    public required decimal Amount { get; init; }
}
