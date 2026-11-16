namespace Ltl.Modules.Pricing.Domain;

/// <summary>
/// 计价规则集（例如"广州—武汉标准价卡"）。版本化粒度单位（ADR-0007）；
/// 停用规则集即停止其全部版本的匹配，历史版本保留。
/// </summary>
public sealed class PricingRuleSet : TenantScopedEntity
{
    /// <summary>规则集编码，租户内唯一（ux_pricing_rule_sets_tenant_code）。</summary>
    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public PricingRuleSetStatus Status { get; private set; }

    private PricingRuleSet()
    {
    }

    /// <exception cref="ArgumentException">tenantId 为空或 code/name 非法时抛出。</exception>
    public static PricingRuleSet Create(
        Guid tenantId,
        string code,
        string name,
        DateTime nowUtc,
        Guid? createdBy = null)
    {
        return new PricingRuleSet
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            Code = RequireNonBlank(code, "规则集编码", 64),
            Name = RequireNonBlank(name, "规则集名称", 200),
            Status = PricingRuleSetStatus.Active,
            CreatedAt = EnsureUtc(nowUtc),
            CreatedBy = createdBy,
        };
    }

    /// <exception cref="ArgumentException">name 非法时抛出。</exception>
    public void Rename(string name, DateTime nowUtc, Guid? updatedBy = null)
    {
        Name = RequireNonBlank(name, "规则集名称", 200);
        Touch(nowUtc, updatedBy);
    }

    /// <summary>停用规则集（幂等）：停止匹配，不删除历史版本。</summary>
    public void Disable(DateTime nowUtc, Guid? updatedBy = null)
    {
        if (Status == PricingRuleSetStatus.Disabled)
        {
            return;
        }

        Status = PricingRuleSetStatus.Disabled;
        Touch(nowUtc, updatedBy);
    }

    /// <summary>启用规则集（幂等）。</summary>
    public void Enable(DateTime nowUtc, Guid? updatedBy = null)
    {
        if (Status == PricingRuleSetStatus.Active)
        {
            return;
        }

        Status = PricingRuleSetStatus.Active;
        Touch(nowUtc, updatedBy);
    }
}
