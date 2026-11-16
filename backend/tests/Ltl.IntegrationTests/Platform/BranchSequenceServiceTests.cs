using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Platform.Application;
using Ltl.Modules.Platform.Domain;
using Ltl.Modules.Tenancy.Domain;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ltl.IntegrationTests.Platform;

/// <summary>
/// SEQ-001 集成测试：并发唯一性（核心）、按日重置、跨网点/跨租户独立、fail-closed。
/// </summary>
public sealed class BranchSequenceServiceTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    private Guid _tenantId;
    private Guid _branchGzId;
    private Guid _branchWhId;

    private async Task SeedAsync()
    {
        var tenant = Tenant.Create($"seq-{Guid.NewGuid():N}", "序号测试租户", DateTime.UtcNow);
        await using (var tenancy = fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenant);
            await tenancy.SaveChangesAsync();
        }

        _tenantId = tenant.Id;

        await using var org = fixture.CreateOrganizationContext(_tenantId);
        var gz = Branch.Create(_tenantId, "GZ01", "广州", DateTime.UtcNow);
        var wh = Branch.Create(_tenantId, "WH02", "武汉", DateTime.UtcNow);
        org.Branches.AddRange(gz, wh);
        await org.SaveChangesAsync();
        _branchGzId = gz.Id;
        _branchWhId = wh.Id;
    }

    private IBranchSequenceService CreateService(Guid? tenantId = null) =>
        new BranchSequenceService(fixture.CreatePlatformContext(tenantId));

    [Fact]
    public async Task Migration_ShouldApplyOnEmptyDatabase()
    {
        await using var context = fixture.CreatePlatformContext();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(0, await context.Sequences.CountAsync());
    }

    [Fact]
    public async Task ConcurrentAllocation_ShouldProduceUniqueContiguousSequence()
    {
        await SeedAsync();
        const int concurrency = 20;
        var date = new DateOnly(2026, 11, 17);

        var numbers = await Task.WhenAll(Enumerable.Range(0, concurrency).Select(_ =>
            Task.Run(async () =>
            {
                await using var context = fixture.CreatePlatformContext(_tenantId);
                var service = new BranchSequenceService(context);
                return await service.NextNumberAsync(_branchGzId, BusinessDocumentType.Waybill, date, "GZ01");
            })));

        // 无重复
        Assert.Equal(concurrency, numbers.Distinct().Count());

        // 序号部分恰为 1..20 连续（每号以 000001..000020 结尾）
        var sequences = numbers
            .Select(n => long.Parse(n[^BusinessNumberFormatter.SequenceDigits..]))
            .OrderBy(x => x)
            .ToArray();
        Assert.Equal(Enumerable.Range(1, concurrency).Select(x => (long)x), sequences);
    }

    [Fact]
    public async Task SameBranchDifferentDays_ShouldResetSequence()
    {
        await SeedAsync();
        var service = CreateService(_tenantId);

        var day1 = await service.NextNumberAsync(_branchGzId, BusinessDocumentType.Waybill, new DateOnly(2026, 11, 17), "GZ01");
        var day2 = await service.NextNumberAsync(_branchGzId, BusinessDocumentType.Waybill, new DateOnly(2026, 11, 18), "GZ01");

        Assert.Equal("YBGZ0120261117000001", day1);
        Assert.Equal("YBGZ0120261118000001", day2);
    }

    [Fact]
    public async Task DifferentBranches_ShouldCountIndependently()
    {
        await SeedAsync();
        var service = CreateService(_tenantId);

        var gz1 = await service.NextNumberAsync(_branchGzId, BusinessDocumentType.Waybill, new DateOnly(2026, 11, 17), "GZ01");
        var gz2 = await service.NextNumberAsync(_branchGzId, BusinessDocumentType.Waybill, new DateOnly(2026, 11, 17), "GZ01");
        var wh1 = await service.NextNumberAsync(_branchWhId, BusinessDocumentType.Waybill, new DateOnly(2026, 11, 17), "WH02");

        Assert.Equal("YBGZ0120261117000001", gz1);
        Assert.Equal("YBGZ0120261117000002", gz2);
        Assert.Equal("YBWH0220261117000001", wh1);
    }

    [Fact]
    public async Task DifferentDocumentTypes_ShouldCountIndependently()
    {
        await SeedAsync();
        var service = CreateService(_tenantId);
        var date = new DateOnly(2026, 11, 17);

        var waybill = await service.NextNumberAsync(_branchGzId, BusinessDocumentType.Waybill, date, "GZ01");
        var transfer = await service.NextNumberAsync(_branchGzId, BusinessDocumentType.BranchTransfer, date, "GZ01");
        var trip = await service.NextNumberAsync(_branchGzId, BusinessDocumentType.TransportTrip, date, "GZ01");

        Assert.Equal("YBGZ0120261117000001", waybill);
        Assert.Equal("DBGZ0120261117000001", transfer);
        Assert.Equal("PCGZ0120261117000001", trip);
    }

    [Fact]
    public async Task DifferentTenants_ShouldCountIndependently()
    {
        await SeedAsync();
        var otherTenant = Tenant.Create($"seq-other-{Guid.NewGuid():N}", "其他租户", DateTime.UtcNow);
        await using (var tenancy = fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(otherTenant);
            await tenancy.SaveChangesAsync();
        }

        await using var otherOrg = fixture.CreateOrganizationContext(otherTenant.Id);
        var otherBranch = Branch.Create(otherTenant.Id, "GZ01", "广州", DateTime.UtcNow);
        otherOrg.Branches.Add(otherBranch);
        await otherOrg.SaveChangesAsync();

        var date = new DateOnly(2026, 11, 17);
        var first = await CreateService(_tenantId).NextNumberAsync(_branchGzId, BusinessDocumentType.Waybill, date, "GZ01");
        var second = await CreateService(otherTenant.Id).NextNumberAsync(otherBranch.Id, BusinessDocumentType.Waybill, date, "GZ01");

        // 各租户独立计数：两个租户的首号相同
        Assert.Equal("YBGZ0120261117000001", first);
        Assert.Equal("YBGZ0120261117000001", second);
    }

    [Fact]
    public async Task Allocation_WithoutTenantContext_ShouldBeRejected()
    {
        await SeedAsync();

        var service = CreateService(null);

        await Assert.ThrowsAsync<TenantContextMissingException>(
            () => service.NextNumberAsync(_branchGzId, BusinessDocumentType.Waybill, new DateOnly(2026, 11, 17), "GZ01"));
    }
}
