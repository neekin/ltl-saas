using System.Text.Json.Serialization;

namespace Ltl.Modules.Pricing.Domain;

/// <summary>
/// 单条匹配条件。全部条件 AND 组合；空条件列表 = 无条件命中（默认规则）。
/// Value/Value2 以字符串保存，按字段类型解析（校验器强制）。
/// </summary>
public sealed class PricingCondition
{
    /// <summary>条件字段（白名单）。</summary>
    public required PricingConditionField Field { get; init; }

    /// <summary>操作符（白名单，与字段兼容性由校验器强制）。</summary>
    public required PricingConditionOperator Operator { get; init; }

    /// <summary>比较值；数值字段为 decimal 文本，Id 字段为 Guid 文本，In 为逗号分隔多值。</summary>
    public required string Value { get; init; }

    /// <summary>仅 Between 使用（闭区间上界）。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Value2 { get; init; }
}
