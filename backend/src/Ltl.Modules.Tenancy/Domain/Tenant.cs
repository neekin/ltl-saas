namespace Ltl.Modules.Tenancy.Domain;

/// <summary>
/// 租户：一家物流企业对应一个租户（AGENTS.md §1）。
/// tenants 是全局表，自身不带 tenant_id；所有租户业务数据通过 ITenantScopedEntity 关联到本实体。
/// </summary>
public sealed class Tenant
{
    public Guid Id { get; private set; }

    /// <summary>租户编码，全局唯一（数据库唯一索引 ux_tenants_code）。</summary>
    public string Code { get; private set; } = null!;

    /// <summary>企业名称。</summary>
    public string Name { get; private set; } = null!;

    public TenantStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    private Tenant()
    {
    }

    /// <exception cref="ArgumentException">code 或 name 为空白时抛出。</exception>
    public static Tenant Create(string code, string name, DateTime nowUtc, Guid? createdBy = null)
    {
        ValidateCode(code);
        ValidateName(name);

        return new Tenant
        {
            Id = Guid.NewGuid(),
            Code = code.Trim(),
            Name = name.Trim(),
            Status = TenantStatus.Active,
            CreatedAt = EnsureUtc(nowUtc),
            CreatedBy = createdBy,
        };
    }

    /// <summary>停用租户（幂等：已停用时无变化）。</summary>
    public void Disable(DateTime nowUtc, Guid? updatedBy = null)
    {
        if (Status == TenantStatus.Disabled)
        {
            return;
        }

        Status = TenantStatus.Disabled;
        Touch(nowUtc, updatedBy);
    }

    /// <summary>启用租户（幂等：已启用时无变化）。</summary>
    public void Enable(DateTime nowUtc, Guid? updatedBy = null)
    {
        if (Status == TenantStatus.Active)
        {
            return;
        }

        Status = TenantStatus.Active;
        Touch(nowUtc, updatedBy);
    }

    private void Touch(DateTime nowUtc, Guid? updatedBy)
    {
        UpdatedAt = EnsureUtc(nowUtc);
        UpdatedBy = updatedBy;
    }

    private static void ValidateCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("租户编码不能为空。", nameof(code));
        }
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("企业名称不能为空。", nameof(name));
        }
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        if (value.Kind == DateTimeKind.Local)
        {
            return value.ToUniversalTime();
        }

        throw new ArgumentException("时间必须携带 UTC 或本地时区信息，禁止 Unspecified。", nameof(value));
    }
}
