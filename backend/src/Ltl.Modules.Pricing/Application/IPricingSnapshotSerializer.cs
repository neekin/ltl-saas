using Ltl.Modules.Pricing.Domain;

namespace Ltl.Modules.Pricing.Application;

/// <summary>
/// 计价快照序列化契约（ADR-0007：快照以强类型 JSONB 存储）。
/// 供跨模块持久化快照（WB-002）使用，避免引用本模块 Infrastructure。
/// </summary>
public interface IPricingSnapshotSerializer
{
    /// <summary>序列化计价输入快照。</summary>
    string SerializeInput(PricingQuoteInput input);

    /// <summary>序列化计价结果快照（含逐项明细解释）。</summary>
    string SerializeQuote(PricingQuoteResult quote);

    /// <summary>反序列化输入快照（审计/重算场景）。</summary>
    PricingQuoteInput DeserializeInput(string json);

    /// <summary>反序列化计价结果快照。</summary>
    PricingQuoteResult DeserializeQuote(string json);
}
