namespace Ltl.Modules.Pricing.Domain;

/// <summary>附加费计费基准（白名单，v1：整票/每件；预留扩展）。</summary>
public enum PricingSurchargeBasis
{
    /// <summary>整票固定。</summary>
    PerWaybill = 1,

    /// <summary>每件。</summary>
    PerPiece = 2,

    /// <summary>按声明价值费率（保价费：声明价值 × Rate%）。</summary>
    PerDeclaredValue = 3,
}
