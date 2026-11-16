using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Routing.Domain;
using Ltl.Modules.Tenancy.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ltl.IntegrationTests.Routing;

/// <summary>
/// ROUTE-001 数据层集成测试：租户范围唯一、跨租户复合外键（网点/方案）、
/// 全局过滤器隔离（fail-closed）、同序号/同网点唯一、xmin 并发。
/// </summary>
public sealed class RoutingPersistenceTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    private Guid _tenantAId;
    private Guid _tenantBId;
    private Guid _branchGzId;
    private Guid _branchWhId;
    private Guid _branchCsId;
    private Guid _branchB_GzId;
    private Guid _branchB_CsId;

    /// <summary>seed 租户甲：广州/武汉/长沙 三网点（对应 ROADMAP 场景 B）。</summary>
    private async Task SeedAsync()
    {
        await using var tenancy = fixture.CreateTenancyContext();
        var tenantA = Tenant.Create($"rt-a-{Guid.NewGuid():N}", "线路测试租户甲", DateTime.UtcNow);
        var tenantB = Tenant.Create($"rt-b-{Guid.NewGuid():N}", "线路测试租户乙", DateTime.UtcNow);
        tenancy.Tenants.AddRange(tenantA, tenantB);
        await tenancy.SaveChangesAsync();
        _tenantAId = tenantA.Id;
        _tenantBId = tenantB.Id;

        await using var org = fixture.CreateOrganizationContext(_tenantAId);
        var gz = Branch.Create(_tenantAId, "gz", "广州", DateTime.UtcNow);
        var wh = Branch.Create(_tenantAId, "wh", "武汉", DateTime.UtcNow);
        var cs = Branch.Create(_tenantAId, "cs", "长沙", DateTime.UtcNow);
        org.Branches.AddRange(gz, wh, cs);
        await org.SaveChangesAsync();
        _branchGzId = gz.Id;
        _branchWhId = wh.Id;
        _branchCsId = cs.Id;

        // 租户乙的网点（FK 测试必须引用本租户网点，跨租户引用会被复合外键拒绝）
        await using var orgB = fixture.CreateOrganizationContext(_tenantBId);
        var bGz = Branch.Create(_tenantBId, "gz", "广州", DateTime.UtcNow);
        var bCs = Branch.Create(_tenantBId, "cs", "长沙", DateTime.UtcNow);
        orgB.Branches.AddRange(bGz, bCs);
        await orgB.SaveChangesAsync();
        _branchB_GzId = bGz.Id;
        _branchB_CsId = bCs.Id;
    }

    private async Task<Route> CreateRouteAsync(Guid tenantId, string code, Guid origin, Guid destination)
    {
        await using var context = fixture.CreateRoutingContext(tenantId);
        var route = Route.Create(tenantId, code, $"线路-{code}", origin, destination, DateTime.UtcNow);
        context.Routes.Add(route);
        await context.SaveChangesAsync();
        return route;
    }

    [Fact]
    public async Task Migration_ShouldApplyOnEmptyDatabase()
    {
        await using var context = fixture.CreateRoutingContext();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(0, await context.Routes.CountAsync());
    }

    [Fact]
    public async Task Route_SameOriginAndDestination_ShouldThrow()
    {
        await SeedAsync();

        await using var context = fixture.CreateRoutingContext(_tenantAId);
        Assert.Throws<ArgumentException>(
            () => Route.Create(_tenantAId, "r001", "空跑线路", _branchGzId, _branchGzId, DateTime.UtcNow));
    }

    [Fact]
    public async Task SameCodeInDifferentTenants_Allowed_DuplicateSameTenantRejected()
    {
        await SeedAsync();
        await CreateRouteAsync(_tenantAId, "r001", _branchGzId, _branchCsId);
        await CreateRouteAsync(_tenantBId, "r001", _branchB_GzId, _branchB_CsId);

        await using var context = fixture.CreateRoutingContext(_tenantAId);
        context.Routes.Add(Route.Create(_tenantAId, "r001", "重复编码", _branchGzId, _branchCsId, DateTime.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task CrossTenantStopBranch_ShouldBeRejectedByCompositeForeignKey()
    {
        await SeedAsync();
        var route = await CreateRouteAsync(_tenantAId, "r001", _branchGzId, _branchCsId);

        // 租户乙的站点引用租户甲的网点 → 跨模块复合外键拒绝
        await using var contextB = fixture.CreateRoutingContext(_tenantBId);
        contextB.Stops.Add(RouteStop.Create(_tenantBId, route.Id, _branchGzId, 0, DateTime.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => contextB.SaveChangesAsync());
    }

    [Fact]
    public async Task CrossTenantSegmentBranch_ShouldBeRejectedByCompositeForeignKey()
    {
        await SeedAsync();
        var route = await CreateRouteAsync(_tenantAId, "r001", _branchGzId, _branchCsId);

        await using var contextA = fixture.CreateRoutingContext(_tenantAId);
        var plan = TransportPlan.Create(_tenantAId, route.Id, "广州—长沙直达", DateTime.UtcNow);
        contextA.Plans.Add(plan);
        await contextA.SaveChangesAsync();

        // 租户乙的区段引用租户甲的方案 → 复合外键拒绝
        await using var contextB = fixture.CreateRoutingContext(_tenantBId);
        contextB.PlanSegments.Add(TransportPlanSegment.Create(
            _tenantBId, plan.Id, 1, _branchGzId, _branchCsId, DateTime.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => contextB.SaveChangesAsync());
    }

    [Fact]
    public async Task GlobalQueryFilter_ShouldIsolateTenants_AndFailClosed()
    {
        await SeedAsync();
        await CreateRouteAsync(_tenantAId, "r001", _branchGzId, _branchCsId);
        await CreateRouteAsync(_tenantBId, "r001", _branchB_GzId, _branchB_CsId);

        await using (var contextA = fixture.CreateRoutingContext(_tenantAId))
        {
            var routes = await contextA.Routes.ToListAsync();
            var route = Assert.Single(routes);
            Assert.Equal(_tenantAId, route.TenantId);
        }

        await using var anonymous = fixture.CreateRoutingContext(null);
        Assert.Empty(await anonymous.Routes.ToListAsync());
    }

    [Fact]
    public async Task SameRouteSameSequence_ShouldBeRejected()
    {
        await SeedAsync();
        var route = await CreateRouteAsync(_tenantAId, "r001", _branchGzId, _branchCsId);

        await using var context = fixture.CreateRoutingContext(_tenantAId);
        context.Stops.Add(RouteStop.Create(_tenantAId, route.Id, _branchGzId, 0, DateTime.UtcNow));
        await context.SaveChangesAsync();

        context.Stops.Add(RouteStop.Create(_tenantAId, route.Id, _branchWhId, 0, DateTime.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task SameRouteSameBranchTwice_ShouldBeRejected()
    {
        await SeedAsync();
        var route = await CreateRouteAsync(_tenantAId, "r001", _branchGzId, _branchCsId);

        await using var context = fixture.CreateRoutingContext(_tenantAId);
        context.Stops.Add(RouteStop.Create(_tenantAId, route.Id, _branchGzId, 0, DateTime.UtcNow));
        await context.SaveChangesAsync();

        context.Stops.Add(RouteStop.Create(_tenantAId, route.Id, _branchGzId, 1, DateTime.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task ConcurrentRouteUpdate_ShouldThrowDbUpdateConcurrencyException()
    {
        await SeedAsync();
        var route = await CreateRouteAsync(_tenantAId, "r001", _branchGzId, _branchCsId);

        await using var contextB = fixture.CreateRoutingContext(_tenantAId);
        await using var contextC = fixture.CreateRoutingContext(_tenantAId);
        var loadedB = await contextB.Routes.SingleAsync(x => x.Id == route.Id);
        var loadedC = await contextC.Routes.SingleAsync(x => x.Id == route.Id);

        loadedB.Disable(DateTime.UtcNow);
        await contextB.SaveChangesAsync();

        loadedC.Disable(DateTime.UtcNow);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextC.SaveChangesAsync());
    }
}
