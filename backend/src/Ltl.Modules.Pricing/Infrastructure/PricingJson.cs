using System.Text.Json;
using System.Text.Json.Serialization;
using Ltl.Modules.Pricing.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Ltl.Modules.Pricing.Infrastructure;

/// <summary>
/// 规则定义 JSON 快照序列化（ADR-0007）。
/// 结构校验由反序列化保证：未知成员、未知枚举字符串、缺失必需成员、null 标注不符一律抛异常。
/// </summary>
public static class PricingJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>definition 列（jsonb）的 EF 值转换器（表达式树不能含 throw，反序列化走独立方法）。</summary>
    public static readonly ValueConverter<PricingRuleDefinition, string> DefinitionConverter = new(
        definition => JsonSerializer.Serialize(definition, Options),
        json => DeserializeDefinition(json));

    private static PricingRuleDefinition DeserializeDefinition(string json) =>
        JsonSerializer.Deserialize<PricingRuleDefinition>(json, Options)
        ?? throw new InvalidOperationException("规则定义快照反序列化结果为 null，数据可能损坏。");
}
