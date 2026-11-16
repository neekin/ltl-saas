namespace Ltl.Modules.Organization.Domain;

/// <summary>角色（租户内）。角色权限与网点数据范围分开建模（AGENTS.md §5.2）。</summary>
public sealed class Role : TenantScopedEntity
{
    /// <summary>角色编码，租户内唯一（ux_roles_tenant_code）。</summary>
    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public RoleStatus Status { get; private set; }

    private Role()
    {
    }

    /// <exception cref="ArgumentException">tenantId 为空或 code/name 非法时抛出。</exception>
    public static Role Create(Guid tenantId, string code, string name, DateTime nowUtc, Guid? createdBy = null)
    {
        return new Role
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            Code = RequireNonBlank(code, "角色编码", 64),
            Name = RequireNonBlank(name, "角色名称", 200),
            Status = RoleStatus.Active,
            CreatedAt = EnsureUtc(nowUtc),
            CreatedBy = createdBy,
        };
    }

    /// <summary>停用角色（幂等）。</summary>
    public void Disable(DateTime nowUtc, Guid? updatedBy = null)
    {
        if (Status == RoleStatus.Disabled)
        {
            return;
        }

        Status = RoleStatus.Disabled;
        Touch(nowUtc, updatedBy);
    }

    /// <summary>启用角色（幂等）。</summary>
    public void Enable(DateTime nowUtc, Guid? updatedBy = null)
    {
        if (Status == RoleStatus.Active)
        {
            return;
        }

        Status = RoleStatus.Active;
        Touch(nowUtc, updatedBy);
    }
}
