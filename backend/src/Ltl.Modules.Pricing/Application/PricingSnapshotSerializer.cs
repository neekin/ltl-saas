using Ltl.Modules.Pricing.Domain;
using Ltl.Modules.Pricing.Infrastructure;

namespace Ltl.Modules.Pricing.Application;

/// <summary>默认实现：使用 ADR-0007 强类型序列化选项（未知成员/枚举拒绝）。</summary>
public sealed class PricingSnapshotSerializer : IPricingSnapshotSerializer
{
    public string SerializeInput(PricingQuoteInput input) =>
        System.Text.Json.JsonSerializer.Serialize(input, PricingJson.Options);

    public string SerializeQuote(PricingQuoteResult quote) =>
        System.Text.Json.JsonSerializer.Serialize(quote, PricingJson.Options);

    public PricingQuoteInput DeserializeInput(string json) =>
        System.Text.Json.JsonSerializer.Deserialize<PricingQuoteInput>(json, PricingJson.Options)
        ?? throw new InvalidOperationException("计价输入快照反序列化结果为 null，数据可能损坏。");

    public PricingQuoteResult DeserializeQuote(string json) =>
        System.Text.Json.JsonSerializer.Deserialize<PricingQuoteResult>(json, PricingJson.Options)
        ?? throw new InvalidOperationException("计价结果快照反序列化结果为 null，数据可能损坏。");
}
