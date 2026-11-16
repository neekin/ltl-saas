namespace Ltl.Modules.Waybills.Domain;

/// <summary>运费支付方式（waybills.payment_type）。术语见 docs/domain/glossary.md。</summary>
public enum WaybillPaymentType
{
    /// <summary>现付：发货时支付运费。</summary>
    Cash = 1,

    /// <summary>到付：货物到达后支付运费。</summary>
    OnDelivery = 2,

    /// <summary>月结：按账期汇总结算（P7 账单）。</summary>
    Monthly = 3,
}
