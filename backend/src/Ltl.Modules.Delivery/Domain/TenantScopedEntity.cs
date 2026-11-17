using Ltl.SharedKernel;

namespace Ltl.Modules.Delivery.Domain;

/// <summary>Delivery 域租户范围实体公共基类（约定同其他模块）。</summary>
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
            throw new ArgumentException("租户 Id 不能为空 Guid。", nameof(tenantId));
        return tenantId;
    }

    protected static DateTime EnsureUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc) return value;
        if (value.Kind == DateTimeKind.Local) return value.ToUniversalTime();
        throw new ArgumentException("时间必须携带 UTC 或本地时区信息，禁止 Unspecified。", nameof(value));
    }
}
