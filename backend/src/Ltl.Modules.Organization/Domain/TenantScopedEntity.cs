using Ltl.SharedKernel;

namespace Ltl.Modules.Organization.Domain;

/// <summary>
/// 组织域租户范围实体的公共基类：租户 Id 与审计字段（AGENTS.md §7）。
/// 实现 ITenantScopedEntity 以应用全局查询过滤器与复合外键约定。
/// </summary>
public abstract class TenantScopedEntity : ITenantScopedEntity
{
    public Guid Id { get; protected set; }

    public Guid TenantId { get; protected set; }

    public DateTime CreatedAt { get; protected set; }

    public Guid? CreatedBy { get; protected set; }

    public DateTime? UpdatedAt { get; protected set; }

    public Guid? UpdatedBy { get; protected set; }

    protected static Guid RequireTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("租户 Id 不能为空 Guid。", nameof(tenantId));
        }

        return tenantId;
    }

    protected static DateTime EnsureUtc(DateTime value)
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

    protected void Touch(DateTime nowUtc, Guid? updatedBy)
    {
        UpdatedAt = EnsureUtc(nowUtc);
        UpdatedBy = updatedBy;
    }

    protected static string RequireNonBlank(string value, string paramName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} 不能为空。", paramName);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"{paramName} 长度不能超过 {maxLength}。", paramName);
        }

        return trimmed;
    }
}
