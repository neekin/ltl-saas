namespace Ltl.Modules.Customers.Domain;

/// <summary>
/// 客户地址（取货/送货地点）。地址以完整文本存储（省市区结构化模型待业务确认后扩展）。
/// 同一客户至多一个默认地址（部分唯一索引 ux_customer_addresses_tenant_customer_default）。
/// </summary>
public sealed class CustomerAddress : TenantScopedEntity
{
    public Guid CustomerId { get; private set; }

    /// <summary>地址用途标签（如"仓库"、"门店"），可选。</summary>
    public string? Label { get; private set; }

    public string Address { get; private set; } = null!;

    public bool IsDefault { get; private set; }

    public string? Remark { get; private set; }

    private CustomerAddress()
    {
    }

    /// <exception cref="ArgumentException">tenantId 为空或 address 为空时抛出。</exception>
    public static CustomerAddress Create(
        Guid tenantId,
        Guid customerId,
        string address,
        DateTime nowUtc,
        string? label = null,
        bool isDefault = false,
        string? remark = null,
        Guid? createdBy = null)
    {
        return new CustomerAddress
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            CustomerId = RequireCustomerId(customerId),
            Label = NormalizeOptional(label, 100),
            Address = RequireNonBlank(address, "地址", 500),
            IsDefault = isDefault,
            Remark = NormalizeOptional(remark, 500),
            CreatedAt = EnsureUtc(nowUtc),
            CreatedBy = createdBy,
        };
    }

    public void Update(string address, string? label, string? remark, DateTime nowUtc, Guid? updatedBy = null)
    {
        Label = NormalizeOptional(label, 100);
        Address = RequireNonBlank(address, "地址", 500);
        Remark = NormalizeOptional(remark, 500);
        Touch(nowUtc, updatedBy);
    }

    private static Guid RequireCustomerId(Guid customerId)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("客户 Id 不能为空 Guid。", nameof(customerId));
        }

        return customerId;
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
            throw new ArgumentException($"内容长度不能超过 {maxLength}。", nameof(value));
        }

        return trimmed;
    }
}
