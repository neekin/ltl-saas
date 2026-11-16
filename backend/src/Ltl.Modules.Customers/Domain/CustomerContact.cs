namespace Ltl.Modules.Customers.Domain;

/// <summary>客户联系人。同一客户至多一个主联系人（部分唯一索引 ux_customer_contacts_tenant_customer_primary）。</summary>
public sealed class CustomerContact : TenantScopedEntity
{
    public Guid CustomerId { get; private set; }

    public string Name { get; private set; } = null!;

    public string Phone { get; private set; } = null!;

    /// <summary>是否主联系人。</summary>
    public bool IsPrimary { get; private set; }

    public string? Remark { get; private set; }

    private CustomerContact()
    {
    }

    /// <exception cref="ArgumentException">tenantId 为空或 name/phone 非法时抛出。</exception>
    public static CustomerContact Create(
        Guid tenantId,
        Guid customerId,
        string name,
        string phone,
        DateTime nowUtc,
        bool isPrimary = false,
        string? remark = null,
        Guid? createdBy = null)
    {
        return new CustomerContact
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            CustomerId = RequireCustomerId(customerId),
            Name = RequireNonBlank(name, "联系人姓名", 100),
            Phone = RequireNonBlank(phone, "联系电话", 50),
            IsPrimary = isPrimary,
            Remark = NormalizeOptional(remark, 500),
            CreatedAt = EnsureUtc(nowUtc),
            CreatedBy = createdBy,
        };
    }

    public void Update(string name, string phone, string? remark, DateTime nowUtc, Guid? updatedBy = null)
    {
        Name = RequireNonBlank(name, "联系人姓名", 100);
        Phone = RequireNonBlank(phone, "联系电话", 50);
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
            throw new ArgumentException($"备注长度不能超过 {maxLength}。", nameof(value));
        }

        return trimmed;
    }
}
