using System.Transactions;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Organization.Infrastructure;
using Ltl.Modules.Platform.Application;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Organization.Application.Admin;

public sealed record RoleDto(Guid Id, string Code, string Name, string Status);

public sealed record RoleDetailDto(
    Guid Id, string Code, string Name, string Status, IReadOnlyList<string> PermissionCodes);

public sealed record PermissionDto(string Code, string Name, string Module);

public sealed record CreateRoleRequest(string Code, string Name);

public sealed record SetPermissionsRequest(IReadOnlyList<string> PermissionCodes);

public interface IRoleAdminService
{
    Task<PageResult<RoleDto>> ListAsync(Guid operatorUserId, int page, int pageSize);
    Task<RoleDetailDto> GetAsync(Guid operatorUserId, Guid roleId);
    Task<RoleDto> CreateAsync(Guid operatorUserId, CreateRoleRequest request);
    Task<RoleDto> RenameAsync(Guid operatorUserId, Guid roleId, string name);
    Task<RoleDto> DisableAsync(Guid operatorUserId, Guid roleId);
    Task<RoleDto> EnableAsync(Guid operatorUserId, Guid roleId);
    Task<RoleDetailDto> SetPermissionsAsync(Guid operatorUserId, Guid roleId, IReadOnlyList<string> permissionCodes);

    /// <summary>平台预置权限点目录（权限配置界面数据源）。</summary>
    Task<IReadOnlyList<PermissionDto>> ListPermissionsAsync(Guid operatorUserId);
}

/// <summary>
/// 角色管理（ORG-003）。权限：role.read / role.manage（实时校验）；
/// 权限点集合仅限平台预置清单（PermissionCodes.All），未知编码 → 400。
/// </summary>
public sealed class RoleAdminService(
    OrganizationDbContext db,
    IAuthorizationService authorization,
    IAuditLogService audit) : AdminServiceBase(db, authorization, audit), IRoleAdminService
{
    public async Task<PageResult<RoleDto>> ListAsync(Guid operatorUserId, int page, int pageSize)
    {
        await RequirePermissionAsync(operatorUserId, PermissionCodes.RoleRead, "organization.role.read");
        var (normalizedPage, normalizedPageSize, skip) = NormalizePaging(page, pageSize);

        var total = await Db.Roles.CountAsync();
        var roles = await Db.Roles
            .OrderBy(r => r.Code)
            .Skip(skip).Take(normalizedPageSize)
            .ToListAsync();

        var items = new List<RoleDto>(roles.Count);
        foreach (var role in roles)
        {
            items.Add(new RoleDto(role.Id, role.Code, role.Name, role.Status.ToString()));
        }

        return new PageResult<RoleDto>(items, total, normalizedPage, normalizedPageSize);
    }

    public async Task<RoleDetailDto> GetAsync(Guid operatorUserId, Guid roleId)
    {
        await RequirePermissionAsync(operatorUserId, PermissionCodes.RoleRead, "organization.role.read");
        var role = await RequireRoleAsync(roleId);
        return await MapDetailAsync(role);
    }

    public async Task<RoleDto> CreateAsync(Guid operatorUserId, CreateRoleRequest request)
    {
        await RequirePermissionAsync(operatorUserId, PermissionCodes.RoleManage, "organization.role.create");
        var tenantId = RequireTenantId();

        if (await Db.Roles.AnyAsync(r => r.Code == request.Code.Trim()))
        {
            throw new ConflictException($"角色编码（{request.Code.Trim()}）已存在。");
        }

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var role = Role.Create(tenantId, request.Code, request.Name, DateTime.UtcNow, operatorUserId);
        Db.Roles.Add(role);
        await Db.SaveChangesAsync();

        await RecordAuditAsync(
            "organization.role.create", "role", role.Id, operatorUserId,
            new { code = role.Code, name = role.Name });
        scope.Complete();

        return new RoleDto(role.Id, role.Code, role.Name, role.Status.ToString());
    }

    public async Task<RoleDto> RenameAsync(Guid operatorUserId, Guid roleId, string name)
    {
        await RequirePermissionAsync(operatorUserId, PermissionCodes.RoleManage, "organization.role.rename");
        var role = await RequireRoleAsync(roleId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        role.Rename(name, DateTime.UtcNow, operatorUserId);
        await Db.SaveChangesAsync();

        await RecordAuditAsync(
            "organization.role.rename", "role", role.Id, operatorUserId,
            new { code = role.Code, name = role.Name });
        scope.Complete();

        return new RoleDto(role.Id, role.Code, role.Name, role.Status.ToString());
    }

    public async Task<RoleDto> DisableAsync(Guid operatorUserId, Guid roleId)
    {
        await RequirePermissionAsync(operatorUserId, PermissionCodes.RoleManage, "organization.role.disable");
        var role = await RequireRoleAsync(roleId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        role.Disable(DateTime.UtcNow, operatorUserId);
        await Db.SaveChangesAsync();

        await RecordAuditAsync(
            "organization.role.disable", "role", role.Id, operatorUserId, new { code = role.Code });
        scope.Complete();

        return new RoleDto(role.Id, role.Code, role.Name, role.Status.ToString());
    }

    public async Task<RoleDto> EnableAsync(Guid operatorUserId, Guid roleId)
    {
        await RequirePermissionAsync(operatorUserId, PermissionCodes.RoleManage, "organization.role.enable");
        var role = await RequireRoleAsync(roleId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        role.Enable(DateTime.UtcNow, operatorUserId);
        await Db.SaveChangesAsync();

        await RecordAuditAsync(
            "organization.role.enable", "role", role.Id, operatorUserId, new { code = role.Code });
        scope.Complete();

        return new RoleDto(role.Id, role.Code, role.Name, role.Status.ToString());
    }

    /// <summary>设置角色权限点集合（差量增删；仅平台预置清单内编码）。</summary>
    public async Task<RoleDetailDto> SetPermissionsAsync(
        Guid operatorUserId, Guid roleId, IReadOnlyList<string> permissionCodes)
    {
        await RequirePermissionAsync(operatorUserId, PermissionCodes.RoleManage, "organization.role.set_permissions");
        var role = await RequireRoleAsync(roleId);

        var target = permissionCodes.Distinct().ToList();
        var unknown = target.Where(code => PermissionCodes.All.All(p => p.Code != code)).ToList();
        if (unknown.Count > 0)
        {
            throw new ArgumentException($"未知权限点：{string.Join("、", unknown)}", nameof(permissionCodes));
        }

        var current = await Db.RolePermissions
            .Where(x => x.RoleId == roleId)
            .Select(x => x.PermissionId)
            .ToListAsync();
        var currentCodes = await Db.Permissions
            .Where(p => current.Contains(p.Id))
            .Select(p => p.Code)
            .ToListAsync();
        var toGrant = target.Except(currentCodes).ToList();
        var toRevoke = currentCodes.Except(target).ToList();

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        foreach (var code in toGrant)
        {
            await Authorization.GrantPermissionAsync(roleId, code);
        }

        foreach (var code in toRevoke)
        {
            await Authorization.RevokePermissionAsync(roleId, code);
        }

        await RecordAuditAsync(
            "organization.role.set_permissions", "role", role.Id, operatorUserId,
            new { code = role.Code, granted = toGrant, revoked = toRevoke });
        var detail = await MapDetailAsync(role);
        scope.Complete();

        return detail;
    }

    /// <summary>权限点目录（只读，来源于迁移预置的全局表；需 role.read）。</summary>
    public async Task<IReadOnlyList<PermissionDto>> ListPermissionsAsync(Guid operatorUserId)
    {
        await RequirePermissionAsync(operatorUserId, PermissionCodes.RoleRead, "organization.permission.list");
        var permissions = await Db.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Module).ThenBy(p => p.Code)
            .ToListAsync();
        return permissions.Select(p => new PermissionDto(p.Code, p.Name, p.Module)).ToList();
    }

    private async Task<Role> RequireRoleAsync(Guid roleId) =>
        await Db.Roles.FindAsync([roleId])
        ?? throw new EntityNotFoundException("角色", roleId);

    private async Task<RoleDetailDto> MapDetailAsync(Role role)
    {
        var permissionIds = await Db.RolePermissions
            .Where(x => x.RoleId == role.Id)
            .Select(x => x.PermissionId)
            .ToListAsync();
        var codes = await Db.Permissions
            .Where(p => permissionIds.Contains(p.Id))
            .Select(p => p.Code)
            .OrderBy(code => code)
            .ToListAsync();

        return new RoleDetailDto(
            role.Id, role.Code, role.Name, role.Status.ToString(), codes);
    }
}
