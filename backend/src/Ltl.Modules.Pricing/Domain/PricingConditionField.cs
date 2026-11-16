namespace Ltl.Modules.Pricing.Domain;

/// <summary>
/// 规则条件字段白名单（AGENTS §6.4：只允许白名单字段）。新增字段 = 追加枚举值 + 校验器/执行器适配。
/// </summary>
public enum PricingConditionField
{
    /// <summary>货物总重量（kg）。</summary>
    WeightKg = 1,

    /// <summary>货物总体积（m³）。</summary>
    VolumeM3 = 2,

    /// <summary>货物件数。</summary>
    PieceCount = 3,

    /// <summary>客户（客户专属规则）。</summary>
    CustomerId = 4,

    /// <summary>线路。</summary>
    RouteId = 5,

    /// <summary>始发网点。</summary>
    OriginBranchId = 6,

    /// <summary>目的网点。</summary>
    DestBranchId = 7,
}
