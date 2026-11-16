using Ltl.Modules.Organization.Domain;

namespace Ltl.Modules.Organization.Application.Authorization;

/// <summary>
/// 组合授权的纯聚合规则（ORG-002 设计文档第 3 节）：
/// - 有效权限 = 启用角色的权限点并集；禁用用户无任何权限；
/// - 有效网点范围 = 租户级范围（全部启用网点）或行级授权 ∩ 启用网点；禁用用户为空。
/// 无状态、可内存测试；实时查询由 AuthorizationService 完成（无缓存 → 禁用即时生效）。
/// </summary>
public static class PermissionEvaluator
{
    public static IReadOnlySet<string> EffectivePermissions(
        IEnumerable<(RoleStatus Status, IReadOnlyCollection<string> Permissions)> roles)
    {
        var permissions = new HashSet<string>();
        foreach (var (status, rolePermissions) in roles)
        {
            if (status != RoleStatus.Active)
            {
                continue;
            }

            foreach (var permission in rolePermissions)
            {
                permissions.Add(permission);
            }
        }

        return permissions;
    }

    public static IReadOnlySet<Guid> EffectiveBranchIds(
        UserStatus userStatus,
        bool tenantWideScope,
        IReadOnlyCollection<(Guid BranchId, BranchStatus Status)> scopedBranches,
        IReadOnlyCollection<(Guid BranchId, BranchStatus Status)> tenantBranches)
    {
        if (userStatus == UserStatus.Disabled)
        {
            return EmptyBranchIds;
        }

        if (tenantWideScope)
        {
            return tenantBranches
                .Where(b => b.Status == BranchStatus.Active)
                .Select(b => b.BranchId)
                .ToHashSet();
        }

        return scopedBranches
            .Where(b => b.Status == BranchStatus.Active)
            .Select(b => b.BranchId)
            .ToHashSet();
    }

    private static readonly IReadOnlySet<Guid> EmptyBranchIds = new HashSet<Guid>();
}
