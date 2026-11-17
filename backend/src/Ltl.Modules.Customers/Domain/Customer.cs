namespace Ltl.Modules.Customers.Domain;

/// <summary>
/// 客户档案。一个客户可授权多个直营网点服务（customer_branch_relations，P2 验收标准）。
/// 不提供物理删除：停用用 Status 表达，历史引用安全。
/// </summary>
public sealed class Customer : TenantScopedEntity
{
    /// <summary>客户编码，租户内唯一（ux_customers_tenant_code）。</summary>
    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? Remark { get; private set; }

    /// <summary>信用额度（月结客户；null = 未设置）。</summary>
    public decimal? CreditLimit { get; private set; }

    /// <summary>账期天数（月结客户；null = 未设置）。</summary>
    public int? PaymentTermsDays { get; private set; }

    public CustomerStatus Status { get; private set; }

    private Customer()
    {
    }

    /// <exception cref="ArgumentException">tenantId 为空或 code/name 非法时抛出。</exception>
    public static Customer Create(
        Guid tenantId,
        string code,
        string name,
        DateTime nowUtc,
        string? remark = null,
        Guid? createdBy = null)
    {
        return new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            Code = RequireNonBlank(code, "客户编码", 64),
            Name = RequireNonBlank(name, "客户名称", 200),
            Remark = NormalizeOptional(remark, 500),
            Status = CustomerStatus.Active,
            CreatedAt = EnsureUtc(nowUtc),
            CreatedBy = createdBy,
        };
    }

    public void UpdateProfile(string name, string? remark, DateTime nowUtc, Guid? updatedBy = null)
    {
        Name = RequireNonBlank(name, "客户名称", 200);
        Remark = NormalizeOptional(remark, 500);
        Touch(nowUtc, updatedBy);
    }

    /// <summary>设置信用额度和账期（月结客户专用）。</summary>
    /// <exception cref="ArgumentException">creditLimit/paymentTermsDays 为负数时抛出。</exception>
    public void SetCreditPolicy(decimal? creditLimit, int? paymentTermsDays, DateTime nowUtc, Guid? updatedBy = null)
    {
        if (creditLimit is < 0)
            throw new ArgumentException("信用额度不能为负数。", nameof(creditLimit));
        if (paymentTermsDays is < 0)
            throw new ArgumentException("账期天数不能为负数。", nameof(paymentTermsDays));
        CreditLimit = creditLimit;
        PaymentTermsDays = paymentTermsDays;
        Touch(nowUtc, updatedBy);
    }

    /// <summary>停用客户（幂等）。</summary>
    public void Disable(DateTime nowUtc, Guid? updatedBy = null)
    {
        if (Status == CustomerStatus.Disabled)
        {
            return;
        }

        Status = CustomerStatus.Disabled;
        Touch(nowUtc, updatedBy);
    }

    /// <summary>启用客户（幂等）。</summary>
    public void Enable(DateTime nowUtc, Guid? updatedBy = null)
    {
        if (Status == CustomerStatus.Active)
        {
            return;
        }

        Status = CustomerStatus.Active;
        Touch(nowUtc, updatedBy);
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"备注长度不能超过 {maxLength}。", nameof(value));
        }

        return trimmed;
    }
}
