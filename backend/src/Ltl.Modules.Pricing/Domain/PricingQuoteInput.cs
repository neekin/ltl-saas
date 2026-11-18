namespace Ltl.Modules.Pricing.Domain;

/// <summary>
/// 计价输入（试算与开单共用）。数值字段由本类校验非负；Id 字段可缺失（缺失时引用该字段的条件不成立）。
/// 本结构同时充当**输入快照**（WB-002 固化费用明细时随运单保存）。
/// </summary>
public sealed class PricingQuoteInput
{
    /// <summary>货物总重量（kg），≥0。</summary>
    public required decimal WeightKg { get; init; }

    /// <summary>货物总体积（m³），≥0。</summary>
    public required decimal VolumeM3 { get; init; }

    /// <summary>货物件数，≥1 整数。</summary>
    public required int PieceCount { get; init; }

    public Guid? CustomerId { get; init; }

    public Guid? RouteId { get; init; }

    public Guid? OriginBranchId { get; init; }

    public Guid? DestBranchId { get; init; }

    /// <summary>声明价值（元，保价计算基准）；null = 未保价，PerDeclaredValue 附加费按 0 计。</summary>
    public decimal? DeclaredValue { get; init; }

    /// <exception cref="ArgumentException">数值非法时抛出。</exception>
    public static void Validate(PricingQuoteInput input)
    {
        if (input.WeightKg < 0)
        {
            throw new ArgumentException("重量不能为负数。", nameof(input));
        }

        if (input.VolumeM3 < 0)
        {
            throw new ArgumentException("体积不能为负数。", nameof(input));
        }

        if (input.PieceCount < 1)
        {
            throw new ArgumentException("件数必须 ≥1。", nameof(input));
        }

        if (input.DeclaredValue is < 0)
        {
            throw new ArgumentException("声明价值不能为负数。", nameof(input));
        }
    }
}
