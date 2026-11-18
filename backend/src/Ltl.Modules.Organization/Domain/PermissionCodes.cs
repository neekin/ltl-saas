namespace Ltl.Modules.Organization.Domain;

/// <summary>
/// 组织域权限点清单（平台预置，迁移 seed）。后续模块在各自任务中按此规范追加权限点。
/// </summary>
public static class PermissionCodes
{
    public const string Module = "organization";

    public const string BranchRead = "organization.branch.read";
    public const string BranchManage = "organization.branch.manage";
    public const string UserRead = "organization.user.read";
    public const string UserManage = "organization.user.manage";
    public const string RoleRead = "organization.role.read";
    public const string RoleManage = "organization.role.manage";

    // ===== P11 增补（NOTIFY-001 / TEN-002）=====
    public const string NotificationRead = "notification.read";
    public const string QuotaManage = "quota.manage";

    /// <summary>权限点固定 Guid（迁移 seed 幂等使用，不得变更）。</summary>
    public static readonly IReadOnlyList<Permission> All = new List<Permission>
    {
        Permission.Create(BranchRead, "查看网点", Module, new Guid("00000000-0000-0000-0000-000000000101")),
        Permission.Create(BranchManage, "管理网点（创建、编辑、启停用）", Module, new Guid("00000000-0000-0000-0000-000000000102")),
        Permission.Create(UserRead, "查看员工", Module, new Guid("00000000-0000-0000-0000-000000000103")),
        Permission.Create(UserManage, "管理员工（创建、编辑、禁用、授权）", Module, new Guid("00000000-0000-0000-0000-000000000104")),
        Permission.Create(RoleRead, "查看角色", Module, new Guid("00000000-0000-0000-0000-000000000105")),
        Permission.Create(RoleManage, "管理角色（创建、编辑、停用、分配权限）", Module, new Guid("00000000-0000-0000-0000-000000000106")),
        Permission.Create(NotificationRead, "查看通知记录", "notification", new Guid("00000000-0000-0000-0000-000000001101")),
        Permission.Create(QuotaManage, "管理租户套餐与配额", "tenancy", new Guid("00000000-0000-0000-0000-000000001102")),
    };
}
