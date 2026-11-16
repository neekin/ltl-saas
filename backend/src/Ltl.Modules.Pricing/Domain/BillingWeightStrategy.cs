namespace Ltl.Modules.Pricing.Domain;

/// <summary>
/// 计费重量策略（AGENTS：重量与体积取大、取小或指定策略）。
/// Max/Min 策略的体积重 = 体积(m³) × VolumeToWeightFactor。
/// </summary>
public enum BillingWeightStrategy
{
    /// <summary>取计费重量与体积重的较大者。</summary>
    WeightVolumeMax = 1,

    /// <summary>取计费重量与体积重的较小者。</summary>
    WeightVolumeMin = 2,

    /// <summary>仅按重量（kg）计费。</summary>
    WeightOnly = 3,

    /// <summary>仅按体积（m³）计费。</summary>
    VolumeOnly = 4,

    /// <summary>仅按件数计费（计费数量 = 件数）。</summary>
    PieceCount = 5,
}
