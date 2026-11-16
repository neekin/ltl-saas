namespace Ltl.Modules.Organization.Domain;

/// <summary>直营网点。同一租户内网点之间是调拨关系，不是对外销售（AGENTS.md §1、§6.3）。</summary>
public sealed class Branch : TenantScopedEntity
{
    /// <summary>网点编码，租户内唯一（ux_branches_tenant_code）。</summary>
    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public BranchStatus Status { get; private set; }

    private Branch()
    {
    }

    /// <exception cref="ArgumentException">tenantId 为空或 code/name 非法时抛出。</exception>
    public static Branch Create(Guid tenantId, string code, string name, DateTime nowUtc, Guid? createdBy = null)
    {
        return new Branch
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            Code = RequireNonBlank(code, "网点编码", 64),
            Name = RequireNonBlank(name, "网点名称", 200),
            Status = BranchStatus.Active,
            CreatedAt = EnsureUtc(nowUtc),
            CreatedBy = createdBy,
        };
    }

    /// <summary>重命名网点（编码不可变）。</summary>
    /// <exception cref="ArgumentException">name 非法时抛出。</exception>
    public void Rename(string name, DateTime nowUtc, Guid? updatedBy = null)
    {
        Name = RequireNonBlank(name, "网点名称", 200);
        Touch(nowUtc, updatedBy);
    }

    /// <summary>停用网点（幂等）。</summary>
    public void Disable(DateTime nowUtc, Guid? updatedBy = null)
    {
        if (Status == BranchStatus.Disabled)
        {
            return;
        }

        Status = BranchStatus.Disabled;
        Touch(nowUtc, updatedBy);
    }

    /// <summary>启用网点（幂等）。</summary>
    public void Enable(DateTime nowUtc, Guid? updatedBy = null)
    {
        if (Status == BranchStatus.Active)
        {
            return;
        }

        Status = BranchStatus.Active;
        Touch(nowUtc, updatedBy);
    }
}
