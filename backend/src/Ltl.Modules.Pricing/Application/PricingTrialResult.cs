using Ltl.Modules.Pricing.Domain;

namespace Ltl.Modules.Pricing.Application;

/// <summary>
/// 试算结果：携带规则版本引用与输入快照回显（WB-002 固化费用明细时随运单保存）。
/// </summary>
public sealed class PricingTrialResult
{
    public required Guid RuleSetId { get; init; }

    public required string RuleSetCode { get; init; }

    /// <summary>命中的已发布版本 Id（计价快照引用）。</summary>
    public required Guid VersionId { get; init; }

    public required int VersionNumber { get; init; }

    /// <summary>输入快照回显。</summary>
    public required PricingQuoteInput Input { get; init; }

    /// <summary>计价结果（命中规则、计费口径、逐项明细、折扣、小计/总额）。</summary>
    public required PricingQuoteResult Quote { get; init; }
}
