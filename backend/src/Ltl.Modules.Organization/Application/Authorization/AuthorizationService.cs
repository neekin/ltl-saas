using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Organization.Infrastructure;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Organization.Application.Authorization;

/// <summary>
/// 组合授权服务实现。授权变更与校验全部实时查询（无缓存 → 禁用即时生效）；
/// 关联表无导航关系（ORG-001），主体存在性检查与关联写入分两步保存。
/// </summary>
public sealed class AuthorizationService(OrganizationDbContext db) : IAuthorizationService
{
    // ===== 授权变更 =====

    public async Task AssignRoleAsync(Guid userId, Guid roleId)
    {
        var user = await RequireUserAsync(userId);
        var role = await RequireRoleAsync(roleId);

        var exists = await db.UserRoles.AnyAsync(x =>
            x.TenantId == user.TenantId && x.UserId == userId && x.RoleId == roleId);
        if (exists)
        {
            return;
        }

        db.UserRoles.Add(UserRole.Create(user.TenantId, userId, roleId));
        await db.SaveChangesAsync();
    }

    public async Task RemoveRoleAsync(Guid userId, Guid roleId)
    {
        var link = await db.UserRoles.SingleOrDefaultAsync(x => x.UserId == userId && x.RoleId == roleId);
        if (link is null)
        {
            return;
        }

        db.UserRoles.Remove(link);
        await db.SaveChangesAsync();
    }

    public async Task GrantBranchScopeAsync(Guid userId, Guid branchId)
    {
        var user = await RequireUserAsync(userId);
        var branch = await RequireBranchAsync(branchId);

        var exists = await db.UserBranchScopes.AnyAsync(x =>
            x.TenantId == user.TenantId && x.UserId == userId && x.BranchId == branchId);
        if (exists)
        {
            return;
        }

        db.UserBranchScopes.Add(UserBranchScope.Create(user.TenantId, userId, branchId));
        await db.SaveChangesAsync();
    }

    public async Task RevokeBranchScopeAsync(Guid userId, Guid branchId)
    {
        var link = await db.UserBranchScopes.SingleOrDefaultAsync(x => x.UserId == userId && x.BranchId == branchId);
        if (link is null)
        {
            return;
        }

        db.UserBranchScopes.Remove(link);
        await db.SaveChangesAsync();
    }

    public async Task GrantTenantWideScopeAsync(Guid userId)
    {
        var user = await RequireUserAsync(userId);
        if (user.TenantWideScope)
        {
            return;
        }

        user.GrantTenantWideScope(DateTime.UtcNow);
        await db.SaveChangesAsync();
    }

    public async Task RevokeTenantWideScopeAsync(Guid userId)
    {
        var user = await RequireUserAsync(userId);
        if (!user.TenantWideScope)
        {
            return;
        }

        user.RevokeTenantWideScope(DateTime.UtcNow);
        await db.SaveChangesAsync();
    }

    public async Task GrantPermissionAsync(Guid roleId, string permissionCode)
    {
        var role = await RequireRoleAsync(roleId);
        var permission = await db.Permissions.SingleOrDefaultAsync(p => p.Code == permissionCode)
            ?? throw new EntityNotFoundException("权限点", permissionCode);

        var exists = await db.RolePermissions.AnyAsync(x =>
            x.TenantId == role.TenantId && x.RoleId == roleId && x.PermissionId == permission.Id);
        if (exists)
        {
            return;
        }

        db.RolePermissions.Add(RolePermission.Create(role.TenantId, roleId, permission.Id));
        await db.SaveChangesAsync();
    }

    public async Task RevokePermissionAsync(Guid roleId, string permissionCode)
    {
        var permissionId = await db.Permissions.Where(p => p.Code == permissionCode)
            .Select(p => p.Id).SingleOrDefaultAsync();
        if (permissionId == Guid.Empty)
        {
            return;
        }

        var link = await db.RolePermissions.SingleOrDefaultAsync(x =>
            x.RoleId == roleId && x.PermissionId == permissionId);
        if (link is null)
        {
            return;
        }

        db.RolePermissions.Remove(link);
        await db.SaveChangesAsync();
    }

    // ===== 校验查询 =====

    public async Task<bool> HasPermissionAsync(Guid userId, string permissionCode)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId);
        if (user is null || user.Status == UserStatus.Disabled)
        {
            return false;
        }

        var roleIds = await db.UserRoles
            .Where(x => x.UserId == userId)
            .Select(x => x.RoleId)
            .ToListAsync();

        // 禁用角色的权限不计入（实时查询 → 禁用即时生效）
        return await db.RolePermissions.AnyAsync(rp =>
            roleIds.Contains(rp.RoleId)
            && db.Roles.Any(r => r.Id == rp.RoleId && r.Status == RoleStatus.Active)
            && db.Permissions.Any(p => p.Id == rp.PermissionId && p.Code == permissionCode));
    }

    public async Task<IReadOnlyCollection<Guid>> GetAuthorizedBranchIdsAsync(Guid userId)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId);
        if (user is null || user.Status == UserStatus.Disabled)
        {
            return Array.Empty<Guid>();
        }

        if (user.TenantWideScope)
        {
            return await db.Branches
                .Where(b => b.Status == BranchStatus.Active)
                .Select(b => b.Id)
                .ToListAsync();
        }

        var scopedBranchIds = await db.UserBranchScopes
            .Where(s => s.UserId == userId)
            .Select(s => s.BranchId)
            .ToListAsync();

        return await db.Branches
            .Where(b => scopedBranchIds.Contains(b.Id) && b.Status == BranchStatus.Active)
            .Select(b => b.Id)
            .ToListAsync();
    }

    public async Task<bool> CanAccessBranchAsync(Guid userId, Guid branchId)
    {
        var authorizedIds = await GetAuthorizedBranchIdsAsync(userId);
        return authorizedIds.Contains(branchId);
    }

    public async Task<bool> AuthorizeAsync(Guid userId, Guid branchId, string permissionCode)
    {
        return await HasPermissionAsync(userId, permissionCode)
            && await CanAccessBranchAsync(userId, branchId);
    }

    // ===== 私有：主体存在性检查（全局过滤器保证只在本租户内查找；跨租户 → 视为不存在） =====

    private async Task<User> RequireUserAsync(Guid userId)
    {
        return await db.Users.SingleOrDefaultAsync(u => u.Id == userId)
            ?? throw new EntityNotFoundException("用户", userId);
    }

    private async Task<Role> RequireRoleAsync(Guid roleId)
    {
        return await db.Roles.SingleOrDefaultAsync(r => r.Id == roleId)
            ?? throw new EntityNotFoundException("角色", roleId);
    }

    private async Task<Branch> RequireBranchAsync(Guid branchId)
    {
        return await db.Branches.SingleOrDefaultAsync(b => b.Id == branchId)
            ?? throw new EntityNotFoundException("网点", branchId);
    }
}
