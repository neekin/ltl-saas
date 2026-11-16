using Ltl.Modules.Customers.Application;
using Ltl.Modules.Customers.Domain;
using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Tenancy.Domain;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ltl.IntegrationTests.Customers;

/// <summary>
/// CUS-001 数据层集成测试：租户范围唯一、跨租户复合外键、部分唯一索引、
/// 全局过滤器隔离（fail-closed）、客户-网点授权服务、xmin 并发。
/// </summary>
public sealed class CustomersPersistenceTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    // seed 后的实际租户 Id（Tenant.Create 内部生成 Id）
    private Guid _tenantAId;
    private Guid _tenantBId;
    private Guid _branchAId;

    private async Task SeedAsync()
    {
        await using var tenancy = fixture.CreateTenancyContext();
        var tenantA = Tenant.Create($"cus-a-{Guid.NewGuid():N}", "客户测试租户甲", DateTime.UtcNow);
        var tenantB = Tenant.Create($"cus-b-{Guid.NewGuid():N}", "客户测试租户乙", DateTime.UtcNow);
        tenancy.Tenants.AddRange(tenantA, tenantB);
        await tenancy.SaveChangesAsync();
        _tenantAId = tenantA.Id;
        _tenantBId = tenantB.Id;

        await using var org = fixture.CreateOrganizationContext(_tenantAId);
        var branch = Branch.Create(_tenantAId, "gz01", "广州营业部", DateTime.UtcNow);
        org.Branches.Add(branch);
        await org.SaveChangesAsync();
        _branchAId = branch.Id;
    }

    private async Task<Customer> CreateCustomerAsync(Guid tenantId, string code)
    {
        await using var context = fixture.CreateCustomersContext(tenantId);
        var customer = Customer.Create(tenantId, code, $"客户-{code}", DateTime.UtcNow);
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer;
    }

    [Fact]
    public async Task Migration_ShouldApplyOnEmptyDatabase()
    {
        await using var context = fixture.CreateCustomersContext();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(0, await context.Customers.CountAsync());
    }

    [Fact]
    public async Task SameCodeInDifferentTenants_ShouldBeAllowed_DuplicateInSameTenantRejected()
    {
        await SeedAsync();
        await CreateCustomerAsync(_tenantAId, "c001");
        await CreateCustomerAsync(_tenantBId, "c001"); // 跨租户同编码允许

        await using var context = fixture.CreateCustomersContext(_tenantAId);
        context.Customers.Add(Customer.Create(_tenantAId, "c001", "重复编码客户", DateTime.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task CrossTenantContact_ShouldBeRejectedByCompositeForeignKey()
    {
        await SeedAsync();
        var customerA = await CreateCustomerAsync(_tenantAId, "c001");

        // 租户 B 的联系人引用租户 A 的客户 → 复合外键拒绝
        await using var contextB = fixture.CreateCustomersContext(_tenantBId);
        contextB.Contacts.Add(CustomerContact.Create(
            _tenantBId, customerA.Id, "李四", "13900139000", DateTime.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => contextB.SaveChangesAsync());
    }

    [Fact]
    public async Task CrossTenantBranchRelation_ShouldBeRejectedByCompositeForeignKey()
    {
        await SeedAsync();
        var customerB = await CreateCustomerAsync(_tenantBId, "c001");

        // 租户 B 的客户引用租户 A 的网点 → 跨模块复合外键拒绝
        await using var contextB = fixture.CreateCustomersContext(_tenantBId);
        contextB.BranchRelations.Add(CustomerBranchRelation.Create(
            _tenantBId, customerB.Id, _branchAId, DateTime.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => contextB.SaveChangesAsync());
    }

    [Fact]
    public async Task GlobalQueryFilter_ShouldIsolateTenants_AndFailClosed()
    {
        await SeedAsync();
        await CreateCustomerAsync(_tenantAId, "c001");
        await CreateCustomerAsync(_tenantBId, "c001");

        await using (var contextA = fixture.CreateCustomersContext(_tenantAId))
        {
            var customers = await contextA.Customers.ToListAsync();
            var customer = Assert.Single(customers);
            Assert.Equal(_tenantAId, customer.TenantId);
        }

        // 无租户上下文 → fail-closed
        await using var anonymous = fixture.CreateCustomersContext(null);
        Assert.Empty(await anonymous.Customers.ToListAsync());
    }

    [Fact]
    public async Task MultiplePrimaryContacts_ShouldBeRejectedByPartialUniqueIndex()
    {
        await SeedAsync();
        var customer = await CreateCustomerAsync(_tenantAId, "c001");

        await using var context = fixture.CreateCustomersContext(_tenantAId);
        context.Contacts.Add(CustomerContact.Create(
            _tenantAId, customer.Id, "王经理", "13800000001", DateTime.UtcNow, isPrimary: true));
        await context.SaveChangesAsync();

        context.Contacts.Add(CustomerContact.Create(
            _tenantAId, customer.Id, "李主管", "13800000002", DateTime.UtcNow, isPrimary: true));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task RelationshipService_ShouldGrantRevokeIdempotently_WithBidirectionalQueries()
    {
        await SeedAsync();
        var customerA = await CreateCustomerAsync(_tenantAId, "c001");
        var customerA2 = await CreateCustomerAsync(_tenantAId, "c002");

        await using var context = fixture.CreateCustomersContext(_tenantAId);
        var service = new CustomerRelationshipService(context);

        await service.GrantBranchServiceAsync(customerA.Id, _branchAId);
        await service.GrantBranchServiceAsync(customerA.Id, _branchAId); // 幂等
        await service.GrantBranchServiceAsync(customerA2.Id, _branchAId);

        // 一个客户可授权多个网点（此处 1 个）、一个网点可服务多个客户
        Assert.Equal(
            new[] { customerA.Id, customerA2.Id }.OrderBy(x => x).ToArray(),
            (await service.GetCustomersServedByBranchAsync(_branchAId)).OrderBy(x => x).ToArray());
        Assert.Equal([_branchAId], (await service.GetServicedBranchIdsAsync(customerA.Id)).ToArray());

        await service.RevokeBranchServiceAsync(customerA.Id, _branchAId);
        Assert.Equal([customerA2.Id],
            (await service.GetCustomersServedByBranchAsync(_branchAId)).ToArray());
    }

    [Fact]
    public async Task RelationshipService_ForeignBranch_ShouldThrowEntityNotFound()
    {
        await SeedAsync();
        var foreignTenant = Tenant.Create($"cus-other-{Guid.NewGuid():N}", "其他租户", DateTime.UtcNow);
        await using (var tenancy = fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(foreignTenant);
            await tenancy.SaveChangesAsync();
        }

        await using var foreignOrg = fixture.CreateOrganizationContext(foreignTenant.Id);
        var foreignBranch = Branch.Create(foreignTenant.Id, "foreign", "其他租户网点", DateTime.UtcNow);
        foreignOrg.Branches.Add(foreignBranch);
        await foreignOrg.SaveChangesAsync();

        var customerA = await CreateCustomerAsync(_tenantAId, "c001");

        await using var context = fixture.CreateCustomersContext(_tenantAId);
        var service = new CustomerRelationshipService(context);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => service.GrantBranchServiceAsync(customerA.Id, foreignBranch.Id));
    }

    [Fact]
    public async Task ConcurrentCustomerUpdate_ShouldThrowDbUpdateConcurrencyException()
    {
        await SeedAsync();
        var customer = await CreateCustomerAsync(_tenantAId, "c001");

        await using var contextB = fixture.CreateCustomersContext(_tenantAId);
        await using var contextC = fixture.CreateCustomersContext(_tenantAId);
        var loadedB = await contextB.Customers.SingleAsync(x => x.Id == customer.Id);
        var loadedC = await contextC.Customers.SingleAsync(x => x.Id == customer.Id);

        loadedB.Disable(DateTime.UtcNow);
        await contextB.SaveChangesAsync();

        loadedC.Disable(DateTime.UtcNow);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextC.SaveChangesAsync());
    }
}
