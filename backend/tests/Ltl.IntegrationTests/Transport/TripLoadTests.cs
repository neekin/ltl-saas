using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ltl.Modules.Customers.Domain;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Platform.Application;
using Ltl.Modules.Pricing.Application;
using Ltl.Modules.Pricing.Domain;
using Ltl.Modules.Tenancy.Domain;
using Ltl.Modules.Transport.Application;
using Ltl.Modules.Transport.Domain;
using Ltl.Modules.Warehouse.Domain;
using Ltl.Modules.Waybills.Application;
using Ltl.Modules.Waybills.Domain;
using Ltl.SharedKernel.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ltl.IntegrationTests.Transport;

/// <summary>
/// TRIP-001 集成测试：批次创建（单号/区段）、配载（货物状态守卫）、防重复装车（数据库强制）、
/// 撤销配载、跨租户车辆校验。
/// </summary>
public class TripLoadTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public TripLoadTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    private HttpClient CreateClient()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("ConnectionStrings:Tenancy", _fixture.ConnectionString);
            });
        return factory.CreateClient();
    }

    /// <summary>seed：租户（GZ01→WH02）+ 车辆（10t/50m³）+ 3 张已确认运单 + 管理员（trip/receive/sort 权限 + 起点范围）。</summary>
    public async Task<TripSeed> SeedAsyncPublic() => await SeedAsync();

    private async Task<TripSeed> SeedAsync()
    {
        var tenantCode = $"trip-{Guid.NewGuid():N}";
        var tenant = Tenant.Create(tenantCode, "运输批次测试租户", DateTime.UtcNow);
        await using (var tenancy = _fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenant);
            await tenancy.SaveChangesAsync();
        }

        await using var org = _fixture.CreateOrganizationContext(tenant.Id);
        var origin = Branch.Create(tenant.Id, "GZ01", "广州", DateTime.UtcNow);
        var dest = Branch.Create(tenant.Id, "WH02", "武汉", DateTime.UtcNow);
        var admin = User.Create(tenant.Id, "admin", "管理员", PasswordHasher.Hash("secret-123"), DateTime.UtcNow);
        var adminRole = Role.Create(tenant.Id, "ops", "管理员", DateTime.UtcNow);
        org.Branches.AddRange(origin, dest);
        org.Users.Add(admin);
        org.Roles.Add(adminRole);
        await org.SaveChangesAsync();

        await using var customers = _fixture.CreateCustomersContext(tenant.Id);
        var customer = Customer.Create(tenant.Id, "KH001", "测试客户", DateTime.UtcNow);
        customers.Customers.Add(customer);
        await customers.SaveChangesAsync();

        var authorization = new AuthorizationService(org);
        foreach (var permission in new[]
                 {
                     TransportPermissionCodes.TripRead, TransportPermissionCodes.TripManage,
                     WarehousePermissionCodes.CargoReceive, WarehousePermissionCodes.CargoSort,
                 })
        {
            await authorization.GrantPermissionAsync(adminRole.Id, permission);
        }

        await authorization.AssignRoleAsync(admin.Id, adminRole.Id);
        await authorization.GrantBranchScopeAsync(admin.Id, origin.Id);

        await using var transport = _fixture.CreateTransportContext(tenant.Id);
        var vehicle = Vehicle.Create(tenant.Id, "粤A12345", "truck", 10000m, 50m, DateTime.UtcNow);
        transport.Vehicles.Add(vehicle);
        await transport.SaveChangesAsync();

        var pricing = new PricingRuleSetService(_fixture.CreatePricingContext(tenant.Id));
        var ruleSet = await pricing.CreateRuleSetAsync("std", "标准价卡", DateTime.UtcNow);
        var version = await pricing.CreateDraftVersionAsync(ruleSet.Id, new PricingRuleDefinition
        {
            Rounding = RoundingPolicy.HalfUpTwoDecimals,
            Rules =
            [
                new PricingRule
                {
                    Priority = 1,
                    Name = "标准运价",
                    Enabled = true,
                    BillingWeightStrategy = BillingWeightStrategy.WeightOnly,
                    Conditions = [],
                    Freight = new PricingFreightFeeItem
                    {
                        Name = "运费",
                        Type = PricingFreightType.ByWeight,
                        UnitPrice = 1.5m,
                    },
                    Surcharges = [],
                },
            ],
        }, DateTime.UtcNow);
        await pricing.PublishVersionAsync(version.Id, DateTime.UtcNow);

        var waybills = new WaybillService(
            _fixture.CreateWaybillsContext(tenant.Id),
            new BranchSequenceService(_fixture.CreatePlatformContext(tenant.Id)),
            new PricingTrialService(_fixture.CreatePricingContext(tenant.Id)),
            new PricingSnapshotSerializer());
        var waybillIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var waybill = await waybills.CreateWaybillAsync(new CreateWaybillCommand(
                origin.Id, dest.Id, customer.Id, WaybillPaymentType.Cash,
                "张三", "13800000000", "广州", "李四", "13900000000", "武汉",
                [new WaybillPackageDraft("纸箱", 1, 100m, 0.5m)],
                ruleSet.Id, DateOnly.FromDateTime(DateTime.UtcNow), OperatorId: admin.Id));
            await waybills.ConfirmWaybillAsync(waybill.WaybillId, origin.Id, admin.Id);
            waybillIds.Add(waybill.WaybillId);
        }

        return new TripSeed(tenant.Id, origin.Id, dest.Id, vehicle.Id, waybillIds, tenantCode, admin.Id);
    }

    public sealed record TripSeed(
        Guid TenantId, Guid OriginBranchId, Guid DestBranchId, Guid VehicleId,
        List<Guid> WaybillIds, string TenantCode, Guid AdminId);

    private async Task<HttpClient> AdminClientAsync(string tenantCode)
    {
        var client = CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { tenant_code = tenantCode, username = "admin", password = "secret-123" });
        login.EnsureSuccessStatusCode();
        var body = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("access_token").GetString());
        return client;
    }

    public async Task ReceivePublicAsync(TripSeed seed, Guid waybillId) => await ReceiveAsync(seed, waybillId);

    private async Task ReceiveAsync(TripSeed seed, Guid waybillId)
    {
        using var client = await AdminClientAsync(seed.TenantCode);
        var receipt = await client.PostAsJsonAsync("/api/warehouse/receipts", new
        {
            waybillId,
            branchId = seed.OriginBranchId,
            receivedPieces = 1,
            receivedWeightKg = 100m,
            receivedVolumeM3 = 0.5m,
            note = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, receipt.StatusCode);
    }

    private async Task<Guid> CreateTripAsync(HttpClient client, TripSeed seed)
    {
        var create = await client.PostAsJsonAsync("/api/transport/trips", new
        {
            originBranchId = seed.OriginBranchId,
            destBranchId = seed.DestBranchId,
            vehicleId = seed.VehicleId,
            driverId = (Guid?)null,
            routeId = (Guid?)null,
            note = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        return (await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Trip_Flow_ShouldEnforceSingleActiveAssignmentPerWaybill()
    {
        var seed = await SeedAsync();
        foreach (var waybillId in seed.WaybillIds.Take(2))
        {
            await ReceiveAsync(seed, waybillId);
        }

        using var client = await AdminClientAsync(seed.TenantCode);

        var trip1 = await CreateTripAsync(client, seed);
        using (var transport = _fixture.CreateTransportContext(seed.TenantId))
        {
            var trip = await transport.Trips.AsNoTracking().SingleAsync(t => t.Id == trip1);
            Assert.StartsWith("PCGZ01", trip.TripNo);
            var segments = await transport.TripSegments
                .AsNoTracking().Where(s => s.TripId == trip1).ToListAsync();
            Assert.Single(segments);
            Assert.Equal(1, segments[0].Seq);
        }

        var load1 = await client.PostAsJsonAsync($"/api/transport/trips/{trip1}/waybills",
            new { waybillId = seed.WaybillIds[0] });
        Assert.Equal(HttpStatusCode.OK, load1.StatusCode);

        var duplicateSameTrip = await client.PostAsJsonAsync($"/api/transport/trips/{trip1}/waybills",
            new { waybillId = seed.WaybillIds[0] });
        Assert.Equal(HttpStatusCode.Conflict, duplicateSameTrip.StatusCode);

        var trip2 = await CreateTripAsync(client, seed);
        var duplicateOtherTrip = await client.PostAsJsonAsync($"/api/transport/trips/{trip2}/waybills",
            new { waybillId = seed.WaybillIds[0] });
        Assert.Equal(HttpStatusCode.Conflict, duplicateOtherTrip.StatusCode);

        var load2 = await client.PostAsJsonAsync($"/api/transport/trips/{trip2}/waybills",
            new { waybillId = seed.WaybillIds[1] });
        Assert.Equal(HttpStatusCode.OK, load2.StatusCode);

        var history = await client.GetAsync($"/api/waybills/{seed.WaybillIds[0]}/trips");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        var historyBody = await JsonDocument.ParseAsync(await history.Content.ReadAsStreamAsync());
        Assert.Equal(1, historyBody.RootElement.GetArrayLength());
        Assert.True(historyBody.RootElement[0].GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task LoadWaybill_NotReceivedWaybill_ShouldReturn409()
    {
        var seed = await SeedAsync();
        await ReceiveAsync(seed, seed.WaybillIds[0]);
        using var client = await AdminClientAsync(seed.TenantCode);
        var trip = await CreateTripAsync(client, seed);

        var rejected = await client.PostAsJsonAsync($"/api/transport/trips/{trip}/waybills",
            new { waybillId = seed.WaybillIds[2] });
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
    }

    [Fact]
    public async Task RemoveWaybill_ShouldAllowReloading()
    {
        var seed = await SeedAsync();
        await ReceiveAsync(seed, seed.WaybillIds[0]);
        using var client = await AdminClientAsync(seed.TenantCode);
        var trip = await CreateTripAsync(client, seed);

        var load = await client.PostAsJsonAsync($"/api/transport/trips/{trip}/waybills",
            new { waybillId = seed.WaybillIds[0] });
        var assignmentId = (await JsonDocument.ParseAsync(await load.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("id").GetGuid();

        var remove = await client.DeleteAsync($"/api/transport/trips/{trip}/waybills/{assignmentId}");
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);

        var reload = await client.PostAsJsonAsync($"/api/transport/trips/{trip}/waybills",
            new { waybillId = seed.WaybillIds[0] });
        Assert.Equal(HttpStatusCode.OK, reload.StatusCode);
    }

    [Fact]
    public async Task CreateTrip_WithForeignVehicle_ShouldReturn404()
    {
        var seed = await SeedAsync();
        var otherTenantCode = $"trip-x-{Guid.NewGuid():N}";
        var otherTenant = Tenant.Create(otherTenantCode, "其他租户", DateTime.UtcNow);
        await using (var tenancy = _fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(otherTenant);
            await tenancy.SaveChangesAsync();
        }

        await using var otherOrg = _fixture.CreateOrganizationContext(otherTenant.Id);
        var otherBranch = Branch.Create(otherTenant.Id, "GZ01", "他租户广州", DateTime.UtcNow);
        var otherAdmin = User.Create(
            otherTenant.Id, "admin", "管理员", PasswordHasher.Hash("secret-123"), DateTime.UtcNow);
        var otherRole = Role.Create(otherTenant.Id, "ops", "管理员", DateTime.UtcNow);
        otherOrg.Branches.Add(otherBranch);
        otherOrg.Users.Add(otherAdmin);
        otherOrg.Roles.Add(otherRole);
        await otherOrg.SaveChangesAsync();
        var otherAuthorization = new AuthorizationService(otherOrg);
        await otherAuthorization.GrantPermissionAsync(otherRole.Id, TransportPermissionCodes.TripManage);
        await otherAuthorization.AssignRoleAsync(otherAdmin.Id, otherRole.Id);

        using var otherClient = await AdminClientAsync(otherTenantCode);
        var notFound = await otherClient.PostAsJsonAsync("/api/transport/trips", new
        {
            originBranchId = otherBranch.Id,
            destBranchId = Guid.NewGuid(),
            vehicleId = seed.VehicleId, // 租户甲的车辆 → 原生 SQL 租户校验 → 404
            driverId = (Guid?)null,
            routeId = (Guid?)null,
            note = (string?)null,
        });
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
    }
}
