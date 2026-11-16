using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Tenancy.Domain;
using Ltl.SharedKernel.Application;
using Ltl.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ltl.IntegrationTests.Organization;

/// <summary>
/// ORG-002 行为级集成测试：组合授权、禁用用户/网点/角色后即时生效、租户隔离、幂等。
/// </summary>
public sealed class AuthorizationServiceTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    // 当前测试实例的租户与主体（SeedAsync 填充）
    private Guid _tenantId;
    private Guid _branchGzId;
    private Guid _branchCsId;
    private Guid _branchDisabledId;
    private Guid _userId;
    private Guid _roleId;

    private AuthorizationService CreateService() => new(fixture.CreateOrganizationContext(_tenantId));

    /// <summary>seed 当前租户：3 个网点（2 启用 1 停用）+ 启用用户 + 启用角色（已分配给用户）。</summary>
    private async Task SeedAsync()
    {
        var tenant = Tenant.Create($"org-auth-{Guid.NewGuid():N}", "授权测试租户", DateTime.UtcNow);
        await using (var tenancy = fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenant);
            await tenancy.SaveChangesAsync();
        }

        _tenantId = tenant.Id;
        _branchGzId = (await SeedBranchAsync("gz01", BranchStatus.Active)).Id;
        _branchCsId = (await SeedBranchAsync("cs01", BranchStatus.Active)).Id;
        _branchDisabledId = (await SeedBranchAsync("hh01", BranchStatus.Disabled)).Id;

        await using var context = fixture.CreateOrganizationContext(_tenantId);
        var user = User.Create(_tenantId, "zhangsan", "张三", PasswordHasher.Hash("secret-123"), DateTime.UtcNow);
        var role = Role.Create(_tenantId, "ops", "运营管理员", DateTime.UtcNow);
        context.Users.Add(user);
        context.Roles.Add(role);
        await context.SaveChangesAsync();

        _userId = user.Id;
        _roleId = role.Id;

        var service = CreateService();
        await service.AssignRoleAsync(_userId, _roleId);
    }

    private async Task<Branch> SeedBranchAsync(string code, BranchStatus status)
    {
        await using var context = fixture.CreateOrganizationContext(_tenantId);
        var branch = Branch.Create(_tenantId, code, $"网点-{code}", DateTime.UtcNow);
        context.Branches.Add(branch);
        await context.SaveChangesAsync();
        if (status == BranchStatus.Disabled)
        {
            branch.Disable(DateTime.UtcNow);
            await context.SaveChangesAsync();
        }

        return branch;
    }

    private async Task<Branch> CreateForeignBranchAsync()
    {
        var foreignTenant = Tenant.Create($"org-other-{Guid.NewGuid():N}", "其他租户", DateTime.UtcNow);
        await using (var tenancy = fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(foreignTenant);
            await tenancy.SaveChangesAsync();
        }

        await using var context = fixture.CreateOrganizationContext(foreignTenant.Id);
        var branch = Branch.Create(foreignTenant.Id, "foreign", "其他租户网点", DateTime.UtcNow);
        context.Branches.Add(branch);
        await context.SaveChangesAsync();
        return branch;
    }

    [Fact]
    public async Task UserWithMultipleBranchScopes_ShouldAccessAllGrantedBranches()
    {
        await SeedAsync();
        var service = CreateService();

        await service.GrantBranchScopeAsync(_userId, _branchGzId);
        await service.GrantBranchScopeAsync(_userId, _branchCsId);

        var authorized = await service.GetAuthorizedBranchIdsAsync(_userId);
        Assert.Equal(new HashSet<Guid> { _branchGzId, _branchCsId }, authorized.ToHashSet());
    }

    [Fact]
    public async Task TenantWideScope_ShouldIncludeAllEnabledBranchesOfOwnTenantOnly()
    {
        await SeedAsync();
        var foreignBranch = await CreateForeignBranchAsync();
        var service = CreateService();

        // 租户级范围必须由服务端显式授予（授权命令）
        await service.GrantTenantWideScopeAsync(_userId);

        var authorized = await service.GetAuthorizedBranchIdsAsync(_userId);

        Assert.Equal(
            new HashSet<Guid> { _branchGzId, _branchCsId },
            authorized.ToHashSet()); // 停用网点 hh01 不在有效范围；其他租户网点不可见
        Assert.DoesNotContain(foreignBranch.Id, authorized);

        // 收回租户级范围后仅剩显式授权网点（本测试未授予行级授权 → 空）
        await service.RevokeTenantWideScopeAsync(_userId);
        Assert.Empty(await service.GetAuthorizedBranchIdsAsync(_userId));
    }

    [Fact]
    public async Task AuthorizationMatrix_ShouldCombinePermissionAndScope()
    {
        await SeedAsync();
        var service = CreateService();

        await service.GrantPermissionAsync(_roleId, PermissionCodes.BranchManage);
        await service.GrantBranchScopeAsync(_userId, _branchGzId);

        // 有权限 + 有范围
        Assert.True(await service.AuthorizeAsync(_userId, _branchGzId, PermissionCodes.BranchManage));
        // 有范围 + 无权限（用未授予的角色权限点）
        Assert.False(await service.AuthorizeAsync(_userId, _branchGzId, PermissionCodes.RoleManage));
        // 无范围 + 有权限
        Assert.False(await service.AuthorizeAsync(_userId, _branchCsId, PermissionCodes.BranchManage));
        // 停用网点 + 有权限（即使未授权也先因范围失败）
        Assert.False(await service.AuthorizeAsync(_userId, _branchDisabledId, PermissionCodes.BranchManage));
    }

    [Fact]
    public async Task DisabledRole_ShouldRevokePermissionImmediately()
    {
        await SeedAsync();
        var service = CreateService();
        await service.GrantPermissionAsync(_roleId, PermissionCodes.BranchManage);

        Assert.True(await service.HasPermissionAsync(_userId, PermissionCodes.BranchManage));

        await using (var context = fixture.CreateOrganizationContext(_tenantId))
        {
            var role = await context.Roles.SingleAsync(r => r.Id == _roleId);
            role.Disable(DateTime.UtcNow);
            await context.SaveChangesAsync();
        }

        Assert.False(await service.HasPermissionAsync(_userId, PermissionCodes.BranchManage));

        await using (var context = fixture.CreateOrganizationContext(_tenantId))
        {
            var role = await context.Roles.SingleAsync(r => r.Id == _roleId);
            role.Enable(DateTime.UtcNow);
            await context.SaveChangesAsync();
        }

        Assert.True(await service.HasPermissionAsync(_userId, PermissionCodes.BranchManage));
    }

    [Fact]
    public async Task DisabledUser_ShouldLosePermissionsAndScopeImmediately()
    {
        await SeedAsync();
        var setupService = CreateService();
        await setupService.GrantPermissionAsync(_roleId, PermissionCodes.BranchManage);
        await setupService.GrantBranchScopeAsync(_userId, _branchGzId);

        await using (var context = fixture.CreateOrganizationContext(_tenantId))
        {
            var user = await context.Users.SingleAsync(u => u.Id == _userId);
            user.Disable(DateTime.UtcNow);
            await context.SaveChangesAsync();
        }

        // "下一次校验" = 新请求 = 新 DbContext：
        // 同一 context 内 EF identity map 会返回已追踪的实体快照（授予时的 Active），
        // 不会反映其他 context 提交的变更；真实请求每次新建 context，语义与本断言一致。
        var verifyService = CreateService();
        Assert.False(await verifyService.HasPermissionAsync(_userId, PermissionCodes.BranchManage));
        Assert.Empty(await verifyService.GetAuthorizedBranchIdsAsync(_userId));
        Assert.False(await verifyService.AuthorizeAsync(_userId, _branchGzId, PermissionCodes.BranchManage));
    }

    [Fact]
    public async Task DisabledBranch_ShouldLeaveEffectiveScopeAndRestoreOnEnable()
    {
        await SeedAsync();
        var service = CreateService();
        await service.GrantBranchScopeAsync(_userId, _branchGzId);
        Assert.True(await service.CanAccessBranchAsync(_userId, _branchGzId));

        await using (var context = fixture.CreateOrganizationContext(_tenantId))
        {
            var branch = await context.Branches.SingleAsync(b => b.Id == _branchGzId);
            branch.Disable(DateTime.UtcNow);
            await context.SaveChangesAsync();
        }

        Assert.False(await service.CanAccessBranchAsync(_userId, _branchGzId));

        await using (var context = fixture.CreateOrganizationContext(_tenantId))
        {
            var branch = await context.Branches.SingleAsync(b => b.Id == _branchGzId);
            branch.Enable(DateTime.UtcNow);
            await context.SaveChangesAsync();
        }

        Assert.True(await service.CanAccessBranchAsync(_userId, _branchGzId));
    }

    [Fact]
    public async Task GrantAndRevoke_ShouldBeIdempotentAndEffective()
    {
        await SeedAsync();
        var service = CreateService();

        await service.GrantPermissionAsync(_roleId, PermissionCodes.BranchRead);
        await service.GrantPermissionAsync(_roleId, PermissionCodes.BranchRead); // 重复授权幂等

        Assert.True(await service.HasPermissionAsync(_userId, PermissionCodes.BranchRead));

        await service.RevokePermissionAsync(_roleId, PermissionCodes.BranchRead);
        await service.RevokePermissionAsync(_roleId, PermissionCodes.BranchRead); // 重复收回幂等

        Assert.False(await service.HasPermissionAsync(_userId, PermissionCodes.BranchRead));

        // 角色移除后权限失效
        await service.GrantPermissionAsync(_roleId, PermissionCodes.UserRead);
        await service.AssignRoleAsync(_userId, _roleId);
        await service.RemoveRoleAsync(_userId, _roleId);
        Assert.False(await service.HasPermissionAsync(_userId, PermissionCodes.UserRead));
    }

    [Fact]
    public async Task CrossTenantAccess_ShouldBeDenied()
    {
        await SeedAsync();
        var foreignBranch = await CreateForeignBranchAsync();
        var service = CreateService();

        await service.GrantPermissionAsync(_roleId, PermissionCodes.BranchManage);

        // 其他租户网点不在有效范围（过滤器隔离）
        Assert.False(await service.CanAccessBranchAsync(_userId, foreignBranch.Id));
        Assert.False(await service.AuthorizeAsync(_userId, foreignBranch.Id, PermissionCodes.BranchManage));

        // 对其他租户网点授权 → 用户查不到 → 数据库层与应用层双重拒绝
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => service.GrantBranchScopeAsync(_userId, foreignBranch.Id));
    }
}
