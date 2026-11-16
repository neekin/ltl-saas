namespace Ltl.Modules.Organization.Domain;

/// <summary>
/// 员工（登录主体）。密码哈希由调用方经 PasswordHasher 生成后传入，领域层不负责口令策略（属登录任务）。
/// </summary>
public sealed class User : TenantScopedEntity
{
    /// <summary>登录名，租户内唯一（ux_users_tenant_username）。</summary>
    public string Username { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    /// <summary>
    /// 是否拥有租户级数据范围（可访问全部网点）。必须由服务端明确授予（AGENTS.md §5.2），
    /// 与 user_branch_scopes 的行级网点授权相互独立。
    /// </summary>
    public bool TenantWideScope { get; private set; }

    public UserStatus Status { get; private set; }

    private User()
    {
    }

    /// <exception cref="ArgumentException">tenantId 为空或 username/displayName/passwordHash 非法时抛出。</exception>
    public static User Create(
        Guid tenantId,
        string username,
        string displayName,
        string passwordHash,
        DateTime nowUtc,
        Guid? createdBy = null,
        bool tenantWideScope = false)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            Username = RequireNonBlank(username, "登录名", 64),
            DisplayName = RequireNonBlank(displayName, "姓名", 200),
            PasswordHash = RequireNonBlank(passwordHash, "密码哈希", 512),
            TenantWideScope = tenantWideScope,
            Status = UserStatus.Active,
            CreatedAt = EnsureUtc(nowUtc),
            CreatedBy = createdBy,
        };
    }

    /// <summary>替换密码哈希（密码策略与验证属登录任务）。</summary>
    public void ResetPassword(string passwordHash, DateTime nowUtc, Guid? updatedBy = null)
    {
        PasswordHash = RequireNonBlank(passwordHash, "密码哈希", 512);
        Touch(nowUtc, updatedBy);
    }

    /// <summary>授予租户级数据范围（服务端显式授权动作，幂等）。</summary>
    public void GrantTenantWideScope(DateTime nowUtc, Guid? updatedBy = null)
    {
        if (TenantWideScope)
        {
            return;
        }

        TenantWideScope = true;
        Touch(nowUtc, updatedBy);
    }

    /// <summary>收回租户级数据范围（幂等）。</summary>
    public void RevokeTenantWideScope(DateTime nowUtc, Guid? updatedBy = null)
    {
        if (!TenantWideScope)
        {
            return;
        }

        TenantWideScope = false;
        Touch(nowUtc, updatedBy);
    }

    /// <summary>禁用用户（幂等）。</summary>
    public void Disable(DateTime nowUtc, Guid? updatedBy = null)
    {
        if (Status == UserStatus.Disabled)
        {
            return;
        }

        Status = UserStatus.Disabled;
        Touch(nowUtc, updatedBy);
    }

    /// <summary>启用用户（幂等）。</summary>
    public void Enable(DateTime nowUtc, Guid? updatedBy = null)
    {
        if (Status == UserStatus.Active)
        {
            return;
        }

        Status = UserStatus.Active;
        Touch(nowUtc, updatedBy);
    }
}
