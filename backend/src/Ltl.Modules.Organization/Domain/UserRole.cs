using Ltl.SharedKernel;

namespace Ltl.Modules.Organization.Domain;

/// <summary>
/// 用户-角色关联（复合主键 (tenant_id, user_id, role_id)）。
/// 复合外键在数据库层保证不引用其他租户的用户/角色（ORG-001 设计文档第 4 节）。
/// </summary>
public sealed class UserRole : ITenantScopedEntity
{
    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid RoleId { get; private set; }

    private UserRole()
    {
    }

    public static UserRole Create(Guid tenantId, Guid userId, Guid roleId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("租户 Id 不能为空 Guid。", nameof(tenantId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("用户 Id 不能为空 Guid。", nameof(userId));
        }

        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("角色 Id 不能为空 Guid。", nameof(roleId));
        }

        return new UserRole { TenantId = tenantId, UserId = userId, RoleId = roleId };
    }
}
