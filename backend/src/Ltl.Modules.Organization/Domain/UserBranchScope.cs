using Ltl.SharedKernel;

namespace Ltl.Modules.Organization.Domain;

/// <summary>
/// 用户网点授权（复合主键 (tenant_id, user_id, branch_id)）。
/// 一个用户可被授权多个网点；租户级范围由 users.tenant_wide_scope 表达（AGENTS.md §5.2）。
/// </summary>
public sealed class UserBranchScope : ITenantScopedEntity
{
    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid BranchId { get; private set; }

    private UserBranchScope()
    {
    }

    public static UserBranchScope Create(Guid tenantId, Guid userId, Guid branchId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("租户 Id 不能为空 Guid。", nameof(tenantId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("用户 Id 不能为空 Guid。", nameof(userId));
        }

        if (branchId == Guid.Empty)
        {
            throw new ArgumentException("网点 Id 不能为空 Guid。", nameof(branchId));
        }

        return new UserBranchScope { TenantId = tenantId, UserId = userId, BranchId = branchId };
    }
}
