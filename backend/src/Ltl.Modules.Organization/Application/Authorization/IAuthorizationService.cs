namespace Ltl.Modules.Organization.Application.Authorization;

/// <summary>
/// 组合授权服务：角色权限（功能权限）× 网点范围（数据范围）。
/// 所有校验实时读取当前数据库状态（无缓存），禁用用户/网点/角色后下一次校验即生效；
/// 所有查询经 OrganizationDbContext 全局查询过滤器限定当前租户。
/// </summary>
public interface IAuthorizationService
{
    // ===== 授权变更 =====

    /// <summary>为用户分配角色（幂等；用户与角色必须在当前租户内）。</summary>
    Task AssignRoleAsync(Guid userId, Guid roleId);

    /// <summary>移除用户角色（幂等：不存在时无操作）。</summary>
    Task RemoveRoleAsync(Guid userId, Guid roleId);

    /// <summary>授权用户访问网点（幂等）。</summary>
    Task GrantBranchScopeAsync(Guid userId, Guid branchId);

    /// <summary>收回网点授权（幂等：不存在时无操作）。</summary>
    Task RevokeBranchScopeAsync(Guid userId, Guid branchId);

    /// <summary>授予用户租户级数据范围（服务端显式授权动作，幂等）。</summary>
    Task GrantTenantWideScopeAsync(Guid userId);

    /// <summary>收回用户租户级数据范围（幂等）。</summary>
    Task RevokeTenantWideScopeAsync(Guid userId);

    /// <summary>为角色授予权限点（幂等；权限点按编码在平台清单中查找）。</summary>
    Task GrantPermissionAsync(Guid roleId, string permissionCode);

    /// <summary>收回角色权限点（幂等）。</summary>
    Task RevokePermissionAsync(Guid roleId, string permissionCode);

    // ===== 校验查询 =====

    /// <summary>用户是否拥有指定权限点（仅启用角色的权限；禁用用户一律 false）。</summary>
    Task<bool> HasPermissionAsync(Guid userId, string permissionCode);

    /// <summary>用户的有效权限点编码集合（实时查询；登录/me 响应用，AUTH-001）。</summary>
    Task<IReadOnlyCollection<string>> GetPermissionCodesAsync(Guid userId);

    /// <summary>用户的有效网点 Id 集合（禁用用户/停用网点被排除）。</summary>
    Task<IReadOnlyCollection<Guid>> GetAuthorizedBranchIdsAsync(Guid userId);

    /// <summary>用户是否可以访问指定网点（本租户、启用、在有效范围内）。</summary>
    Task<bool> CanAccessBranchAsync(Guid userId, Guid branchId);

    /// <summary>组合判断：拥有权限点且可访问网点。</summary>
    Task<bool> AuthorizeAsync(Guid userId, Guid branchId, string permissionCode);
}
