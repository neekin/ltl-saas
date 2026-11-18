using Ltl.Modules.Customers.Domain;
using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Routing.Domain;
using Ltl.Modules.Tenancy.Domain;
using Ltl.Modules.Waybills.Domain;
using Ltl.Modules.Waybills.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ltl.IntegrationTests.Waybills;

/// <summary>
/// WB-001 集成测试：迁移、运单号租户内唯一、跨租户复合外键、过滤器隔离 + fail-closed、
/// 检查约束、件号唯一、金额/重量/体积精度往返、xmin 并发。
/// </summary>
public sealed class WaybillPersistenceTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    private Guid _tenantId;
    private Guid _originBranchId;
    private Guid _destBranchId;
    private Guid _customerId;

    private async Task SeedAsync()
    {
        var tenant = Tenant.Create($"wb-{Guid.NewGuid():N}", "运单测试租户", DateTime.UtcNow);
        await using (var tenancy = fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenant);
            await tenancy.SaveChangesAsync();
        }

        _tenantId = tenant.Id;

        await using var org = fixture.CreateOrganizationContext(_tenantId);
        var origin = Branch.Create(_tenantId, "GZ01", "广州", DateTime.UtcNow);
        var dest = Branch.Create(_tenantId, "WH02", "武汉", DateTime.UtcNow);
        org.Branches.AddRange(origin, dest);
        await org.SaveChangesAsync();
        _originBranchId = origin.Id;
        _destBranchId = dest.Id;

        await using var customers = fixture.CreateCustomersContext(_tenantId);
        var customer = Customer.Create(_tenantId, "KH001", "测试客户", DateTime.UtcNow);
        customers.Customers.Add(customer);
        await customers.SaveChangesAsync();
        _customerId = customer.Id;
    }

    private Waybill CreateWaybill(
        string waybillNo = "YBGZ0120261117000001",
        IReadOnlyList<WaybillPackageDraft>? packages = null) =>
        Waybill.Create(
            _tenantId, waybillNo, _originBranchId, _destBranchId, _customerId,
            WaybillPaymentType.Cash, "张三", "13800000000", "广州市白云区XX街1号",
            "李四", "13900000000", "武汉市汉阳区YY路2号",
            packages ?? [new WaybillPackageDraft("纸箱", 2, 12.345m, 0.1234m)],
            DateTime.UtcNow);

    [Fact]
    public async Task Migration_ShouldApplyOnEmptyDatabase()
    {
        await using var context = fixture.CreateWaybillsContext();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(0, await context.Waybills.CountAsync());
    }

    [Fact]
    public async Task SaveAndReload_ShouldPersistAggregateWithPackages()
    {
        await SeedAsync();
        await using var context = fixture.CreateWaybillsContext(_tenantId);
        var waybill = CreateWaybill(packages:
        [
            new WaybillPackageDraft("纸箱", 2, 12.345m, 0.1234m),
            new WaybillPackageDraft("木架", 1, 30.5m, 0.5m),
        ]);
        context.Waybills.Add(waybill);
        await context.SaveChangesAsync();

        await using var verification = fixture.CreateWaybillsContext(_tenantId);
        var reloaded = await verification.Waybills.Include(w => w.Packages)
            .SingleAsync(w => w.Id == waybill.Id);

        Assert.Equal("YBGZ0120261117000001", reloaded.WaybillNo);
        Assert.Equal(2, reloaded.Packages.Count);
        Assert.Equal(12.345m, reloaded.Packages[0].WeightKg); // numeric(12,3) 精度往返
        Assert.Equal(0.1234m, reloaded.Packages[0].VolumeM3); // numeric(12,4) 精度往返
        Assert.Equal(3, reloaded.TotalPieces);
        Assert.Equal(55.19m, reloaded.TotalWeightKg); // 乘件数口径：12.345×2 + 30.5×1
        Assert.Equal(WaybillCodStatus.None, reloaded.CodStatus);
    }

    [Fact]
    public async Task DuplicateWaybillNo_SameTenant_ShouldBeRejected()
    {
        await SeedAsync();
        await using var context = fixture.CreateWaybillsContext(_tenantId);
        context.Waybills.Add(CreateWaybill());
        await context.SaveChangesAsync();

        context.Waybills.Add(CreateWaybill());

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task SameWaybillNo_AcrossTenants_ShouldBeAllowed()
    {
        await SeedAsync();
        var tenantB = Tenant.Create($"wb-b-{Guid.NewGuid():N}", "租户乙", DateTime.UtcNow);
        await using (var tenancy = fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenantB);
            await tenancy.SaveChangesAsync();
        }

        await using var orgB = fixture.CreateOrganizationContext(tenantB.Id);
        var originB = Branch.Create(tenantB.Id, "GZ01", "广州", DateTime.UtcNow);
        var destB = Branch.Create(tenantB.Id, "WH02", "武汉", DateTime.UtcNow);
        orgB.Branches.AddRange(originB, destB);
        await orgB.SaveChangesAsync();

        await using var customersB = fixture.CreateCustomersContext(tenantB.Id);
        var customerB = Customer.Create(tenantB.Id, "KH001", "客户乙", DateTime.UtcNow);
        customersB.Customers.Add(customerB);
        await customersB.SaveChangesAsync();

        await using var contextB = fixture.CreateWaybillsContext(tenantB.Id);
        contextB.Waybills.Add(Waybill.Create(
            tenantB.Id, "YBGZ0120261117000001", originB.Id, destB.Id, customerB.Id,
            WaybillPaymentType.Cash, "赵六", "13600000000", "甲地", "钱七", "13700000000", "乙地",
            [new WaybillPackageDraft("货", 1, 1m, 0.01m)], DateTime.UtcNow));
        await contextB.SaveChangesAsync();

        Assert.Equal(1, await contextB.Waybills.CountAsync());
    }

    [Fact]
    public async Task CrossTenantReferences_ShouldBeRejectedByCompositeForeignKeys()
    {
        await SeedAsync();
        var tenantB = Tenant.Create($"wb-b-{Guid.NewGuid():N}", "租户乙", DateTime.UtcNow);
        await using (var tenancy = fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenantB);
            await tenancy.SaveChangesAsync();
        }

        await using var orgB = fixture.CreateOrganizationContext(tenantB.Id);
        var branchB = Branch.Create(tenantB.Id, "GZ01", "广州", DateTime.UtcNow);
        orgB.Branches.Add(branchB);
        await orgB.SaveChangesAsync();

        // 租户乙的运单引用租户甲的网点 → 复合外键拒绝
        await using var customersB = fixture.CreateCustomersContext(tenantB.Id);
        var customerB = Customer.Create(tenantB.Id, "KH-B", "客户乙", DateTime.UtcNow);
        customersB.Customers.Add(customerB);
        await customersB.SaveChangesAsync();

        await using var contextB = fixture.CreateWaybillsContext(tenantB.Id);
        contextB.Waybills.Add(Waybill.Create(
            tenantB.Id, "WB-B1", branchB.Id, _destBranchId, customerB.Id,
            WaybillPaymentType.Cash, "赵六", "1", "甲地", "钱七", "2", "乙地",
            [new WaybillPackageDraft("货", 1, 1m, 0.01m)], DateTime.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => contextB.SaveChangesAsync());
    }

    [Fact]
    public async Task PackageReferencingOtherTenantWaybill_ShouldBeRejected()
    {
        await SeedAsync();
        var tenantB = Tenant.Create($"wb-b-{Guid.NewGuid():N}", "租户乙", DateTime.UtcNow);
        await using (var tenancy = fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenantB);
            await tenancy.SaveChangesAsync();
        }

        var waybillA = CreateWaybill();
        await using (var contextA = fixture.CreateWaybillsContext(_tenantId))
        {
            contextA.Waybills.Add(waybillA);
            await contextA.SaveChangesAsync();
        }

        // 租户乙的货物件引用租户甲的运单 → 复合外键拒绝（23503）
        var foreignPackage = WaybillPackage.Create(
            tenantB.Id, waybillA.Id, 1, new WaybillPackageDraft("他租户件", 1, 1m, 0.01m), DateTime.UtcNow, null);
        await using var contextB = fixture.CreateWaybillsContext(tenantB.Id);
        contextB.Packages.Add(foreignPackage);

        await Assert.ThrowsAsync<DbUpdateException>(() => contextB.SaveChangesAsync());
    }

    [Fact]
    public async Task TenantIsolation_ShouldFilterWaybillsAndFailClosed()
    {
        await SeedAsync();
        var tenantB = Tenant.Create($"wb-iso-{Guid.NewGuid():N}", "隔离租户", DateTime.UtcNow);
        await using (var tenancy = fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenantB);
            await tenancy.SaveChangesAsync();
        }

        // 租户乙 seed 自己的网点与客户（复合外键要求：不能引用租户甲的资源）
        await using var orgB = fixture.CreateOrganizationContext(tenantB.Id);
        var originB = Branch.Create(tenantB.Id, "GZ01", "广州", DateTime.UtcNow);
        var destB = Branch.Create(tenantB.Id, "WH02", "武汉", DateTime.UtcNow);
        orgB.Branches.AddRange(originB, destB);
        await orgB.SaveChangesAsync();

        await using var customersB = fixture.CreateCustomersContext(tenantB.Id);
        var customerB = Customer.Create(tenantB.Id, "KH-B", "客户乙", DateTime.UtcNow);
        customersB.Customers.Add(customerB);
        await customersB.SaveChangesAsync();

        await using (var contextA = fixture.CreateWaybillsContext(_tenantId))
        {
            contextA.Waybills.Add(CreateWaybill());
            await contextA.SaveChangesAsync();
        }

        await using (var contextB = fixture.CreateWaybillsContext(tenantB.Id))
        {
            contextB.Waybills.Add(Waybill.Create(
                tenantB.Id, "WB-B1", originB.Id, destB.Id, customerB.Id,
                WaybillPaymentType.Cash, "赵六", "1", "甲地", "钱七", "2", "乙地",
                [new WaybillPackageDraft("货", 1, 1m, 0.01m)], DateTime.UtcNow));
            await contextB.SaveChangesAsync();
        }

        // 租户甲只见自己的运单
        await using var contextA2 = fixture.CreateWaybillsContext(_tenantId);
        Assert.Single(await contextA2.Waybills.ToListAsync());

        // 无租户上下文 → fail-closed（0 行）
        await using var contextNone = fixture.CreateWaybillsContext();
        Assert.Empty(await contextNone.Waybills.ToListAsync());
    }

    [Fact]
    public async Task InvalidPaymentType_ShouldBeRejectedByCheckConstraint()
    {
        await SeedAsync();

        // 绕过领域模型直插非法 payment_type（99）→ 数据库检查约束兜底拒绝
        await using var context = fixture.CreateWaybillsContext(_tenantId);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
            context.Database.ExecuteSqlRawAsync(
                "INSERT INTO waybills (id, tenant_id, waybill_no, origin_branch_id, dest_branch_id, customer_id, " +
                "payment_type, waybill_status, cargo_status, transport_status, delivery_status, freight_status, cod_status, " +
                "sender_name, sender_phone, sender_address, receiver_name, receiver_phone, receiver_address, created_at) " +
                "VALUES ({0}, {1}, 'WB-RAW', {2}, {3}, {4}, 99, 1, 1, 1, 1, 1, 1, 's', '1', 'a', 'r', '2', 'b', now())",
                Guid.NewGuid(), _tenantId, _originBranchId, _destBranchId, _customerId));
    }

    [Fact]
    public async Task SamePackageNoOnSameWaybill_ShouldBeRejected()
    {
        await SeedAsync();
        var waybill = CreateWaybill();
        await using (var context = fixture.CreateWaybillsContext(_tenantId))
        {
            context.Waybills.Add(waybill);
            await context.SaveChangesAsync();
        }

        await using var context2 = fixture.CreateWaybillsContext(_tenantId);
        context2.Packages.Add(WaybillPackage.Create(
            _tenantId, waybill.Id, 1, new WaybillPackageDraft("重复件号", 1, 1m, 0.01m), DateTime.UtcNow, null));

        await Assert.ThrowsAsync<DbUpdateException>(() => context2.SaveChangesAsync());
    }

    [Fact]
    public async Task ConcurrentRename_ShouldTriggerXminConcurrency()
    {
        await SeedAsync();
        var waybill = CreateWaybill();
        await using (var context = fixture.CreateWaybillsContext(_tenantId))
        {
            context.Waybills.Add(waybill);
            await context.SaveChangesAsync();
        }

        await using var context1 = fixture.CreateWaybillsContext(_tenantId);
        await using var context2 = fixture.CreateWaybillsContext(_tenantId);

        var loaded1 = await context1.Waybills.SingleAsync(w => w.Id == waybill.Id);
        var loaded2 = await context2.Waybills.SingleAsync(w => w.Id == waybill.Id);

        loaded1.ApplyPricing(88.5m, DateTime.UtcNow, null);
        await context1.SaveChangesAsync();

        loaded2.ApplyPricing(99m, DateTime.UtcNow, null);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context2.SaveChangesAsync());
    }
}
