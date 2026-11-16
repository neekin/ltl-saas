namespace Ltl.Modules.Pricing.Domain;

/// <summary>
/// 金额舍入策略（白名单）。第一版仅允许保留 2 位小数、四舍五入（AwayFromZero）；后续扩展追加枚举值。
/// </summary>
public enum RoundingPolicy
{
    /// <summary>保留 2 位小数，MidpointRounding.AwayFromZero。</summary>
    HalfUpTwoDecimals = 1,
}
