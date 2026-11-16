using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Warehouse.Domain;
using Ltl.Modules.Customers.Domain;
using Ltl.Modules.Transport.Domain;
using Ltl.Modules.Waybills.Domain;
using Ltl.Modules.Tenancy.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ltl.IntegrationTests.Organization;

/// <summary>
/// ORG-001 数据层集成测试：租户范围唯一约束、跨租户复合外键、全局查询过滤器隔离（fail-closed）、
/// xmin 乐观并发。使用 PostgreSqlFixture（迁移已在夹具初始化时应用）。
/// </summary>
public sealed class OrganizationPersistenceTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    // tenants 行的实际 Id（Tenant.Create 内部生成 Id，seed 后才能用于关联表）
    private Guid _tenantAId;
    private Guid _tenantBId;

    private async Task SeedTwoTenantsAsync()
    {
        await using var tenancy = fixture.CreateTenancyContext();
        // tenants.code 全局唯一：使用随机编码避免测试间冲突
        var tenantA = Tenant.Create($"org-a-{Guid.NewGuid():N}", "测试租户甲", DateTime.UtcNow);
        var tenantB = Tenant.Create($"org-b-{Guid.NewGuid():N}", "测试租户乙", DateTime.UtcNow);
        tenancy.Tenants.AddRange(tenantA, tenantB);
        await tenancy.SaveChangesAsync();
        _tenantAId = tenantA.Id;
        _tenantBId = tenantB.Id;
    }

    [Fact]
    public async Task Migration_ShouldApplyWithPermissionSeed()
    {
        await using var context = fixture.CreateOrganizationContext();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        // 平台预置权限点已 seed（组织域 + 各业务模块追加：仓储域 RECEIVE-001）
        var permissionCount = await context.Permissions.CountAsync();
        Assert.Equal(
            PermissionCodes.All.Count
            + WarehousePermissionCodes.All.Count
            + WaybillPermissionCodes.All.Count
            + TransportPermissionCodes.All.Count
            + CustomerPermissionCodes.All.Count,
            permissionCount);
    }

    [Fact]
    public async Task SameCodeInDifferentTenants_ShouldBeAllowed()
    {
        await SeedTwoTenantsAsync();

        await using var contextA = fixture.CreateOrganizationContext(_tenantAId);
        await using var contextB = fixture.CreateOrganizationContext(_tenantBId);

        contextA.Branches.Add(Branch.Create(_tenantAId, "gz01", "租户甲广州营业部", DateTime.UtcNow));
        await contextA.SaveChangesAsync();

        // 同一网点编码在另一租户下允许（租户范围唯一约束）
        contextB.Branches.Add(Branch.Create(_tenantBId, "gz01", "租户乙广州营业部", DateTime.UtcNow));
        await contextB.SaveChangesAsync();
    }

    [Fact]
    public async Task DuplicateCodeInSameTenant_ShouldBeRejected()
    {
        await SeedTwoTenantsAsync();

        await using var context = fixture.CreateOrganizationContext(_tenantAId);
        context.Branches.Add(Branch.Create(_tenantAId, "dup01", "第一个网点", DateTime.UtcNow));
        await context.SaveChangesAsync();

        context.Branches.Add(Branch.Create(_tenantAId, "dup01", "重复编码网点", DateTime.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task CrossTenantBranchScope_ShouldBeRejectedByCompositeForeignKey()
    {
        await SeedTwoTenantsAsync();

        await using var contextA = fixture.CreateOrganizationContext(_tenantAId);
        await using var contextB = fixture.CreateOrganizationContext(_tenantBId);

        var branchA = Branch.Create(_tenantAId, "gz01", "租户甲广州营业部", DateTime.UtcNow);
        contextA.Branches.Add(branchA);
        await contextA.SaveChangesAsync();

        var userB = User.Create(_tenantBId, "lisi", "租户乙李四", "pbkdf2-sha256$100000$abcdefghij123456==$hash", DateTime.UtcNow);
        contextB.Users.Add(userB);
        await contextB.SaveChangesAsync();

        // 租户乙的用户引用租户甲的网点 → 数据库层复合外键拒绝（AGENTS.md §5.1）
        contextB.UserBranchScopes.Add(UserBranchScope.Create(_tenantBId, userB.Id, branchA.Id));

        await Assert.ThrowsAsync<DbUpdateException>(() => contextB.SaveChangesAsync());
    }

    [Fact]
    public async Task GlobalQueryFilter_ShouldIsolateTenants()
    {
        await SeedTwoTenantsAsync();

        await using (var contextA = fixture.CreateOrganizationContext(_tenantAId))
        {
            contextA.Branches.Add(Branch.Create(_tenantAId, "gz01", "租户甲广州", DateTime.UtcNow));
            await contextA.SaveChangesAsync();
        }

        await using (var contextB = fixture.CreateOrganizationContext(_tenantBId))
        {
            contextB.Branches.Add(Branch.Create(_tenantBId, "gz01", "租户乙广州", DateTime.UtcNow));
            await contextB.SaveChangesAsync();
        }

        await using (var contextA = fixture.CreateOrganizationContext(_tenantAId))
        {
            var branches = await contextA.Branches.ToListAsync();
            var branch = Assert.Single(branches);
            Assert.Equal(_tenantAId, branch.TenantId);
        }

        await using (var contextB = fixture.CreateOrganizationContext(_tenantBId))
        {
            var branches = await contextB.Branches.ToListAsync();
            var branch = Assert.Single(branches);
            Assert.Equal(_tenantBId, branch.TenantId);
        }
    }

    [Fact]
    public async Task GlobalQueryFilter_WithoutTenantContext_ShouldReturnNoRows()
    {
        await SeedTwoTenantsAsync();

        await using var contextA = fixture.CreateOrganizationContext(_tenantAId);
        contextA.Branches.Add(Branch.Create(_tenantAId, "gz01", "租户甲广州", DateTime.UtcNow));
        await contextA.SaveChangesAsync();

        // 无租户上下文 → fail-closed：返回 0 行
        await using var anonymousContext = fixture.CreateOrganizationContext(null);
        var branches = await anonymousContext.Branches.ToListAsync();
        Assert.Empty(branches);
    }

    [Fact]
    public async Task DuplicateUserBranchScope_ShouldBeRejectedByCompositePrimaryKey()
    {
        await SeedTwoTenantsAsync();

        // 注意：关联表与主体表之间无 EF 导航关系（外键仅存在于数据库层），
        // 必须先保存主体再保存关联，EF 不保证跨实体插入顺序。
        await using var context = fixture.CreateOrganizationContext(_tenantAId);
        var branch = Branch.Create(_tenantAId, "gz01", "租户甲广州", DateTime.UtcNow);
        var user = User.Create(_tenantAId, "zhangsan", "张三", "pbkdf2-sha256$100000$abcdefghij123456==$hash", DateTime.UtcNow);
        context.Branches.Add(branch);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await using var scopeContext = fixture.CreateOrganizationContext(_tenantAId);
        scopeContext.UserBranchScopes.Add(UserBranchScope.Create(_tenantAId, user.Id, branch.Id));
        await scopeContext.SaveChangesAsync();

        // 用新上下文重复插入相同复合主键（同一上下文会先触发追踪冲突，无法验证数据库层）
        await using var duplicateContext = fixture.CreateOrganizationContext(_tenantAId);
        duplicateContext.UserBranchScopes.Add(UserBranchScope.Create(_tenantAId, user.Id, branch.Id));

        await Assert.ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());
    }

    [Fact]
    public async Task ConcurrentBranchUpdate_ShouldThrowDbUpdateConcurrencyException()
    {
        await SeedTwoTenantsAsync();

        await using var writer = fixture.CreateOrganizationContext(_tenantAId);
        var branch = Branch.Create(_tenantAId, "cc01", "并发网点", DateTime.UtcNow);
        writer.Branches.Add(branch);
        await writer.SaveChangesAsync();

        await using var contextB = fixture.CreateOrganizationContext(_tenantAId);
        await using var contextC = fixture.CreateOrganizationContext(_tenantAId);
        var loadedB = await contextB.Branches.SingleAsync(x => x.Id == branch.Id);
        var loadedC = await contextC.Branches.SingleAsync(x => x.Id == branch.Id);

        loadedB.Disable(DateTime.UtcNow);
        await contextB.SaveChangesAsync();

        loadedC.Disable(DateTime.UtcNow);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextC.SaveChangesAsync());
    }
}
