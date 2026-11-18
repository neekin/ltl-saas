using System.Security.Claims;
using Ltl.Modules.Organization.Application.Admin;
using Ltl.Modules.Organization.Application.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ltl.Api.Endpoints;

/// <summary>
/// 组织管理端点（ORG-003，首批受保护业务 API）。端点为薄壳：解析当前用户（JWT sub claim）
/// → 调用模块服务（权限点实时校验在服务层）。actor_user_id 来自服务端 claim，不信任 Body。
/// </summary>
public static class OrganizationEndpoints
{
    public static void MapOrganizationEndpoints(this WebApplication app)
    {
        var branches = app.MapGroup("/api/organization/branches");
        branches.MapGet("/", async (int? page, int? pageSize, ClaimsPrincipal user, IBranchAdminService service) =>
            Results.Ok(await service.ListAsync(RequireUserId(user), page ?? 1, pageSize ?? 20)));
        branches.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IBranchAdminService service) =>
            Results.Ok(await service.GetAsync(RequireUserId(user), id)));
        branches.MapPost("/", async (CreateBranchRequest request, ClaimsPrincipal user, IBranchAdminService service) =>
            Results.Ok(await service.CreateAsync(RequireUserId(user), request)));
        branches.MapPut("/{id:guid}", async (Guid id, RenameRequest request, ClaimsPrincipal user, IBranchAdminService service) =>
            Results.Ok(await service.RenameAsync(RequireUserId(user), id, request.Name)));
        branches.MapPost("/{id:guid}/disable", async (Guid id, ClaimsPrincipal user, IBranchAdminService service) =>
            Results.Ok(await service.DisableAsync(RequireUserId(user), id)));
        branches.MapPost("/{id:guid}/enable", async (Guid id, ClaimsPrincipal user, IBranchAdminService service) =>
            Results.Ok(await service.EnableAsync(RequireUserId(user), id)));

        var users = app.MapGroup("/api/organization/users");
        users.MapGet("/", async (int? page, int? pageSize, ClaimsPrincipal user, IUserAdminService service) =>
            Results.Ok(await service.ListAsync(RequireUserId(user), page ?? 1, pageSize ?? 20)));
        users.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IUserAdminService service) =>
            Results.Ok(await service.GetAsync(RequireUserId(user), id)));
        users.MapPost("/", async (CreateUserRequest request, ClaimsPrincipal user, IUserAdminService service) =>
            Results.Ok(await service.CreateAsync(RequireUserId(user), request)));
        users.MapPut("/{id:guid}/name", async (Guid id, RenameRequest request, ClaimsPrincipal user, IUserAdminService service) =>
            Results.Ok(await service.RenameAsync(RequireUserId(user), id, request.Name)));
        users.MapPost("/{id:guid}/reset-password", async (Guid id, ResetPasswordRequest request, ClaimsPrincipal user, IUserAdminService service) =>
            Results.Ok(await service.ResetPasswordAsync(RequireUserId(user), id, request.Password)));
        users.MapPost("/{id:guid}/disable", async (Guid id, ClaimsPrincipal user, IUserAdminService service) =>
            Results.Ok(await service.DisableAsync(RequireUserId(user), id)));
        users.MapPost("/{id:guid}/enable", async (Guid id, ClaimsPrincipal user, IUserAdminService service) =>
            Results.Ok(await service.EnableAsync(RequireUserId(user), id)));
        users.MapPut("/{id:guid}/roles", async (Guid id, SetRolesRequest request, ClaimsPrincipal user, IUserAdminService service) =>
            Results.Ok(await service.SetRolesAsync(RequireUserId(user), id, request.RoleIds)));
        users.MapPut("/{id:guid}/branch-scopes", async (Guid id, SetBranchScopesRequest request, ClaimsPrincipal user, IUserAdminService service) =>
            Results.Ok(await service.SetBranchScopesAsync(RequireUserId(user), id, request.BranchIds)));
        users.MapPost("/{id:guid}/tenant-wide-scope", async (Guid id, ClaimsPrincipal user, IUserAdminService service) =>
            Results.Ok(await service.GrantTenantWideScopeAsync(RequireUserId(user), id)));
        users.MapDelete("/{id:guid}/tenant-wide-scope", async (Guid id, ClaimsPrincipal user, IUserAdminService service) =>
            Results.Ok(await service.RevokeTenantWideScopeAsync(RequireUserId(user), id)));

        var roles = app.MapGroup("/api/organization/roles");
        roles.MapGet("/", async (int? page, int? pageSize, ClaimsPrincipal user, IRoleAdminService service) =>
            Results.Ok(await service.ListAsync(RequireUserId(user), page ?? 1, pageSize ?? 20)));
        roles.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IRoleAdminService service) =>
            Results.Ok(await service.GetAsync(RequireUserId(user), id)));
        roles.MapPost("/", async (CreateRoleRequest request, ClaimsPrincipal user, IRoleAdminService service) =>
            Results.Ok(await service.CreateAsync(RequireUserId(user), request)));
        roles.MapPut("/{id:guid}", async (Guid id, RenameRequest request, ClaimsPrincipal user, IRoleAdminService service) =>
            Results.Ok(await service.RenameAsync(RequireUserId(user), id, request.Name)));
        roles.MapPost("/{id:guid}/disable", async (Guid id, ClaimsPrincipal user, IRoleAdminService service) =>
            Results.Ok(await service.DisableAsync(RequireUserId(user), id)));
        roles.MapPost("/{id:guid}/enable", async (Guid id, ClaimsPrincipal user, IRoleAdminService service) =>
            Results.Ok(await service.EnableAsync(RequireUserId(user), id)));
        roles.MapPut("/{id:guid}/permissions", async (Guid id, SetPermissionsRequest request, ClaimsPrincipal user, IRoleAdminService service) =>
            Results.Ok(await service.SetPermissionsAsync(RequireUserId(user), id, request.PermissionCodes)));
        roles.MapGet("/permissions", async (ClaimsPrincipal user, IRoleAdminService service) =>
            Results.Ok(await service.ListPermissionsAsync(RequireUserId(user))));
    }

    /// <summary>从 JWT sub claim 解析操作者（服务端来源，AGENTS §5.1；缺失 → 认证失败 401 语义）。</summary>
    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new AuthenticationFailedException("无效的认证凭据");
}
