using Ltl.SharedKernel;

namespace Ltl.Modules.Organization.Domain;

/// <summary>角色-权限点关联（复合主键 (tenant_id, role_id, permission_id)）。</summary>
public sealed class RolePermission : ITenantScopedEntity
{
    public Guid TenantId { get; private set; }

    public Guid RoleId { get; private set; }

    public Guid PermissionId { get; private set; }

    private RolePermission()
    {
    }

    public static RolePermission Create(Guid tenantId, Guid roleId, Guid permissionId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("租户 Id 不能为空 Guid。", nameof(tenantId));
        }

        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("角色 Id 不能为空 Guid。", nameof(roleId));
        }

        if (permissionId == Guid.Empty)
        {
            throw new ArgumentException("权限点 Id 不能为空 Guid。", nameof(permissionId));
        }

        return new RolePermission { TenantId = tenantId, RoleId = roleId, PermissionId = permissionId };
    }
}
