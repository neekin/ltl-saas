using System.Transactions;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Organization.Infrastructure;
using Ltl.Modules.Platform.Application;
using Ltl.SharedKernel.Application;
using Ltl.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Organization.Application.Admin;

public sealed record UserDto(
    Guid Id, string Username, string DisplayName, string Status, bool TenantWideScope);

public sealed record UserDetailDto(
    Guid Id, string Username, string DisplayName, string Status, bool TenantWideScope,
    IReadOnlyList<Guid> RoleIds, IReadOnlyList<Guid> BranchScopeIds);

public sealed record CreateUserRequest(string Username, string DisplayName, string Password);

public sealed record ResetPasswordRequest(string Password);

public sealed record SetRolesRequest(IReadOnlyList<Guid> RoleIds);

public sealed record SetBranchScopesRequest(IReadOnlyList<Guid> BranchIds);

public interface IUserAdminService
{
    Task<PageResult<UserDto>> ListAsync(Guid operatorUserId, int page, int pageSize);
    Task<UserDetailDto> GetAsync(Guid operatorUserId, Guid userId);
    Task<UserDto> CreateAsync(Guid operatorUserId, CreateUserRequest request);
    Task<UserDto> RenameAsync(Guid operatorUserId, Guid userId, string displayName);
    Task<UserDto> ResetPasswordAsync(Guid operatorUserId, Guid userId, string newPassword);
    Task<UserDto> DisableAsync(Guid operatorUserId, Guid userId);
    Task<UserDto> EnableAsync(Guid operatorUserId, Guid userId);
    Task<UserDetailDto> SetRolesAsync(Guid operatorUserId, Guid userId, IReadOnlyList<Guid> roleIds);
    Task<UserDetailDto> SetBranchScopesAsync(Guid operatorUserId, Guid userId, IReadOnlyList<Guid> branchIds);
    Task<UserDetailDto> GrantTenantWideScopeAsync(Guid operatorUserId, Guid userId);
    Task<UserDetailDto> RevokeTenantWideScopeAsync(Guid operatorUserId, Guid userId);
}

/// <summary>
/// 员工管理（ORG-003）。权限：user.read / user.manage（实时校验）；
/// 密码经 PasswordPolicy 校验 + PasswordHasher 哈希（明文不落库/不入日志审计，AGENTS §11）；
/// 自保护：禁止禁用自己的账号（ORG-003 设计 §5）。
/// </summary>
public sealed class UserAdminService(
    OrganizationDbContext db,
    IAuthorizationService authorization,
    IAuditLogService audit) : AdminServiceBase(db, authorization, audit), IUserAdminService
{
    public async Task<PageResult<UserDto>> ListAsync(Guid operatorUserId, int page, int pageSize)
    {
        await RequirePermissionAsync(operatorUserId, PermissionCodes.UserRead, "organization.user.read");
        var (normalizedPage, normalizedPageSize, skip) = NormalizePaging(page, pageSize);

        var total = await Db.Users.CountAsync();
        var items = await Db.Users
            .OrderBy(u => u.Username)
            .Skip(skip).Take(normalizedPageSize)
            .ToListAsync();

        return new PageResult<UserDto>(
            items.Select(Map).ToList(), total, normalizedPage, normalizedPageSize);
    }

    public async Task<UserDetailDto> GetAsync(Guid operatorUserId, Guid userId)
    {
        await RequirePermissionAsync(operatorUserId, PermissionCodes.UserRead, "organization.user.read");
        var user = await RequireUserAsync(userId);
        return await MapDetailAsync(user);
    }

    public async Task<UserDto> CreateAsync(Guid operatorUserId, CreateUserRequest request)
    {
        // 套餐配额前置校验（TEN-002）：原生 SQL 读租户配额（同一物理库，AGENTS §5.1 模式）
        await EnsureUserQuotaAsync();

        await RequirePermissionAsync(operatorUserId, PermissionCodes.UserManage, "organization.user.create");
        var tenantId = RequireTenantId();

        var policyError = PasswordPolicy.Validate(request.Password);
        if (policyError is not null)
        {
            throw new ArgumentException(policyError, nameof(request));
        }

        if (await Db.Users.AnyAsync(u => u.Username == request.Username.Trim()))
        {
            throw new ConflictException($"登录名（{request.Username.Trim()}）已存在。");
        }

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var user = User.Create(
            tenantId, request.Username, request.DisplayName,
            PasswordHasher.Hash(request.Password), DateTime.UtcNow, operatorUserId);
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        await RecordAuditAsync(
            "organization.user.create", "user", user.Id, operatorUserId,
            new { username = user.Username, displayName = user.DisplayName });
        scope.Complete();

        return Map(user);
    }

    public async Task<UserDto> RenameAsync(Guid operatorUserId, Guid userId, string displayName)
    {
        await RequirePermissionAsync(operatorUserId, PermissionCodes.UserManage, "organization.user.rename");
        var user = await RequireUserAsync(userId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        user.Rename(displayName, DateTime.UtcNow, operatorUserId);
        await Db.SaveChangesAsync();

        await RecordAuditAsync(
            "organization.user.rename", "user", user.Id, operatorUserId,
            new { username = user.Username, displayName = user.DisplayName });
        scope.Complete();

        return Map(user);
    }

    public async Task<UserDto> ResetPasswordAsync(Guid operatorUserId, Guid userId, string newPassword)
    {
        await RequirePermissionAsync(operatorUserId, PermissionCodes.UserManage, "organization.user.reset_password");
        var user = await RequireUserAsync(userId);

        var policyError = PasswordPolicy.Validate(newPassword);
        if (policyError is not null)
        {
            throw new ArgumentException(policyError, nameof(newPassword));
        }

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        user.ResetPassword(PasswordHasher.Hash(newPassword), DateTime.UtcNow, operatorUserId);
        await Db.SaveChangesAsync();

        // 审计不含密码或哈希（AGENTS §11）
        await RecordAuditAsync(
            "organization.user.reset_password", "user", user.Id, operatorUserId,
            new { username = user.Username });
        scope.Complete();

        return Map(user);
    }

    public async Task<UserDto> DisableAsync(Guid operatorUserId, Guid userId)
    {
        await RequirePermissionAsync(operatorUserId, PermissionCodes.UserManage, "organization.user.disable");

        // 自保护：禁止禁用自己（防自锁，ORG-003 设计 §5）
        if (operatorUserId == userId)
        {
            throw new PermissionDeniedException("不能禁用自己的账号");
        }

        var user = await RequireUserAsync(userId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        user.Disable(DateTime.UtcNow, operatorUserId);
        await Db.SaveChangesAsync();

        await RecordAuditAsync(
            "organization.user.disable", "user", user.Id, operatorUserId,
            new { username = user.Username });
        scope.Complete();

        return Map(user);
    }

    public async Task<UserDto> EnableAsync(Guid operatorUserId, Guid userId)
    {
        await RequirePermissionAsync(operatorUserId, PermissionCodes.UserManage, "organization.user.enable");
        var user = await RequireUserAsync(userId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        user.Enable(DateTime.UtcNow, operatorUserId);
        await Db.SaveChangesAsync();

        await RecordAuditAsync(
            "organization.user.enable", "user", user.Id, operatorUserId,
            new { username = user.Username });
        scope.Complete();

        return Map(user);
    }

    /// <summary>设置角色集合（与当前差量增删；角色存在性由授权服务校验）。</summary>
    public async Task<UserDetailDto> SetRolesAsync(
        Guid operatorUserId, Guid userId, IReadOnlyList<Guid> roleIds)
    {
        await RequirePermissionAsync(operatorUserId, PermissionCodes.UserManage, "organization.user.set_roles");
        var user = await RequireUserAsync(userId);

        var current = await Db.UserRoles
            .Where(x => x.UserId == userId)
            .Select(x => x.RoleId)
            .ToListAsync();
        var target = roleIds.Distinct().ToList();
        var toAdd = target.Except(current).ToList();
        var toRemove = current.Except(target).ToList();

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        foreach (var roleId in toAdd)
        {
            await authorization.AssignRoleAsync(userId, roleId);
        }

        foreach (var roleId in toRemove)
        {
            await authorization.RemoveRoleAsync(userId, roleId);
        }

        await RecordAuditAsync(
            "organization.user.set_roles", "user", user.Id, operatorUserId,
            new { username = user.Username, added = toAdd, removed = toRemove });
        var detail = await MapDetailAsync(user);
        scope.Complete();

        return detail;
    }

    /// <summary>设置网点范围集合（差量增删；网点存在性由授权服务校验）。</summary>
    public async Task<UserDetailDto> SetBranchScopesAsync(
        Guid operatorUserId, Guid userId, IReadOnlyList<Guid> branchIds)
    {
        await RequirePermissionAsync(operatorUserId, PermissionCodes.UserManage, "organization.user.set_branch_scopes");
        var user = await RequireUserAsync(userId);

        var current = await Db.UserBranchScopes
            .Where(x => x.UserId == userId)
            .Select(x => x.BranchId)
            .ToListAsync();
        var target = branchIds.Distinct().ToList();
        var toAdd = target.Except(current).ToList();
        var toRemove = current.Except(target).ToList();

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        foreach (var branchId in toAdd)
        {
            await authorization.GrantBranchScopeAsync(userId, branchId);
        }

        foreach (var branchId in toRemove)
        {
            await authorization.RevokeBranchScopeAsync(userId, branchId);
        }

        await RecordAuditAsync(
            "organization.user.set_branch_scopes", "user", user.Id, operatorUserId,
            new { username = user.Username, added = toAdd, removed = toRemove });
        var detail = await MapDetailAsync(user);
        scope.Complete();

        return detail;
    }

    public async Task<UserDetailDto> GrantTenantWideScopeAsync(Guid operatorUserId, Guid userId)
    {
        await RequirePermissionAsync(operatorUserId, PermissionCodes.UserManage, "organization.user.grant_tenant_wide_scope");
        var user = await RequireUserAsync(userId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        await authorization.GrantTenantWideScopeAsync(userId);
        await RecordAuditAsync(
            "organization.user.grant_tenant_wide_scope", "user", user.Id, operatorUserId,
            new { username = user.Username });
        var detail = await MapDetailAsync(user);
        scope.Complete();

        return detail;
    }

    public async Task<UserDetailDto> RevokeTenantWideScopeAsync(Guid operatorUserId, Guid userId)
    {
        await RequirePermissionAsync(operatorUserId, PermissionCodes.UserManage, "organization.user.revoke_tenant_wide_scope");
        var user = await RequireUserAsync(userId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        await authorization.RevokeTenantWideScopeAsync(userId);
        await RecordAuditAsync(
            "organization.user.revoke_tenant_wide_scope", "user", user.Id, operatorUserId,
            new { username = user.Username });
        var detail = await MapDetailAsync(user);
        scope.Complete();

        return detail;
    }

    private async Task<User> RequireUserAsync(Guid userId) =>
        await Db.Users.FindAsync([userId])
        ?? throw new EntityNotFoundException("员工", userId);

    private async Task<UserDetailDto> MapDetailAsync(User user)
    {
        var roleIds = await Db.UserRoles
            .Where(x => x.UserId == user.Id)
            .Select(x => x.RoleId)
            .OrderBy(x => x)
            .ToListAsync();
        var branchScopeIds = await Db.UserBranchScopes
            .Where(x => x.UserId == user.Id)
            .Select(x => x.BranchId)
            .OrderBy(x => x)
            .ToListAsync();

        return new UserDetailDto(
            user.Id, user.Username, user.DisplayName, user.Status.ToString(),
            user.TenantWideScope, roleIds, branchScopeIds);
    }

    private static UserDto Map(User user) => new(
        user.Id, user.Username, user.DisplayName, user.Status.ToString(), user.TenantWideScope);

    /// <summary>用户配额前置校验（TEN-002）。</summary>
    private async Task EnsureUserQuotaAsync()
    {
        var tenantId = db.CurrentTenantId
            ?? throw new TenantContextMissingException();
        var rows = await db.Database
            .SqlQuery<TenantQuotaRow>(
                $"SELECT max_users AS \"MaxUsers\" FROM tenants WHERE id = {tenantId}")
            .ToListAsync();
        var max = rows.SingleOrDefault()?.MaxUsers;
        if (max is { } limit)
        {
            var current = await db.Users.IgnoreQueryFilters().CountAsync(u => u.TenantId == tenantId);
            if (current >= limit)
            {
                throw new ConflictException($"套餐用户配额已用满（{current}/{limit}），请升级套餐后重试。");
            }
        }
    }
    private sealed record TenantQuotaRow(int? MaxUsers);
}
