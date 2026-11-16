using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text.Json;
using Ltl.Modules.Waybills.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ltl.IntegrationTests.Transport;

/// <summary>
/// TRIP-002 集成测试：发车（运输状态联动/空车拒绝）、到达（配载转非活跃/多段装车）、
/// 发车后不可配载/取消、费用追加式、状态守卫。
/// </summary>
public class TripDepartArriveTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public TripDepartArriveTests(PostgreSqlFixture fixture)
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

    private async Task<(Guid TenantId, Guid OriginBranchId, Guid DestBranchId, Guid VehicleId, Guid WaybillId, string TenantCode)>
        SeedReceivedWaybillAsync()
    {
        var loadTests = new TripLoadTests(_fixture);
        var seed = await loadTests.SeedAsyncPublic();
        await loadTests.ReceivePublicAsync(seed, seed.WaybillIds[0]);
        return (seed.TenantId, seed.OriginBranchId, seed.DestBranchId, seed.VehicleId, seed.WaybillIds[0], seed.TenantCode);
    }

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

    private async Task<(Guid TripId, Guid WaybillId, Guid TenantId, Guid OriginBranchId, Guid DestBranchId, string TenantCode)>
        CreateLoadedTripAsync()
    {
        var (tenantId, originBranchId, destBranchId, vehicleId, waybillId, tenantCode) =
            await SeedReceivedWaybillAsync();
        using var client = await AdminClientAsync(tenantCode);

        var create = await client.PostAsJsonAsync("/api/transport/trips", new
        {
            originBranchId = originBranchId,
            destBranchId = destBranchId,
            vehicleId = vehicleId,
            driverId = (Guid?)null,
            routeId = (Guid?)null,
            note = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var tripId = (await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("id").GetGuid();

        var load = await client.PostAsJsonAsync($"/api/transport/trips/{tripId}/waybills",
            new { waybillId });
        Assert.Equal(HttpStatusCode.OK, load.StatusCode);

        return (tripId, waybillId, tenantId, originBranchId, destBranchId, tenantCode);
    }

    [Fact]
    public async Task Depart_ShouldMarkWaybillsShipped_AndEmptyTripRejected()
    {
        var (tripId, waybillId, tenantId, originBranchId, destBranchId, tenantCode) = await CreateLoadedTripAsync();
        using var client = await AdminClientAsync(tenantCode);
        var otherSeed = await new TripLoadTests(_fixture).SeedAsyncPublic();

        // 空批次发车 → 409（同租户另一批次，未配载）
        var emptyTripId = Guid.NewGuid();
        await using (var transport = _fixture.CreateTransportContext(tenantId))
        {
            var vehicle = await transport.Vehicles.AsNoTracking().SingleAsync(v => v.PlateNo == "粤A12345");
            var emptyTrip = Ltl.Modules.Transport.Domain.TransportTrip.Create(
                tenantId, $"PCEMPTY{Guid.NewGuid():N}".Substring(0, 24), destBranchId, originBranchId,
                vehicle.Id, null, null, null, DateTime.UtcNow);
            transport.Trips.Add(emptyTrip);
            await transport.SaveChangesAsync();
            emptyTripId = emptyTrip.Id;
        }

        var emptyDepart = await client.PostAsync($"/api/transport/trips/{emptyTripId}/depart", null);
        Assert.Equal(HttpStatusCode.Conflict, emptyDepart.StatusCode);

        // 发车 → 200；运输状态 Shipped + 事件 Departed
        var depart = await client.PostAsync($"/api/transport/trips/{tripId}/depart", null);
        Assert.Equal(HttpStatusCode.OK, depart.StatusCode);

        await using var waybills = _fixture.CreateWaybillsContext(tenantId);
        var waybill = await waybills.Waybills.AsNoTracking().SingleAsync(w => w.Id == waybillId);
        Assert.Equal(WaybillTransportStatus.Shipped, waybill.TransportStatus);
        Assert.True(await waybills.Events.AnyAsync(
            e => e.WaybillId == waybillId && e.EventType == WaybillEventType.Departed));

        // 重复发车 → 409
        var again = await client.PostAsync($"/api/transport/trips/{tripId}/depart", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        // 发车后配载 → 409（批次状态守卫）
        var load = await client.PostAsJsonAsync($"/api/transport/trips/{tripId}/waybills",
            new { waybillId = otherSeed.WaybillIds[0] });
        Assert.Equal(HttpStatusCode.Conflict, load.StatusCode);

        // "已发运不可取消"守卫真实生效（WB-001）
        var waybillsService = new Ltl.Modules.Waybills.Application.WaybillService(
            _fixture.CreateWaybillsContext(tenantId),
            new Ltl.Modules.Platform.Application.BranchSequenceService(_fixture.CreatePlatformContext(tenantId)),
            new Ltl.Modules.Pricing.Application.PricingTrialService(_fixture.CreatePricingContext(tenantId)),
            new Ltl.Modules.Pricing.Application.PricingSnapshotSerializer());
        await Assert.ThrowsAsync<Ltl.Modules.Waybills.Domain.WaybillDomainException>(
            () => waybillsService.CancelWaybillAsync(waybillId, "尝试取消已发运运单"));
    }

    [Fact]
    public async Task Arrive_ShouldDeactivateAssignments_AllowNextLegLoading()
    {
        var (tripId, waybillId, tenantId, originBranchId, destBranchId, tenantCode) = await CreateLoadedTripAsync();
        using var client = await AdminClientAsync(tenantCode);

        // 未发车到达 → 409
        var premature = await client.PostAsync($"/api/transport/trips/{tripId}/arrive", null);
        Assert.Equal(HttpStatusCode.Conflict, premature.StatusCode);

        await client.PostAsync($"/api/transport/trips/{tripId}/depart", null);

        // 到达 → 200；配载行 inactive；事件 Arrived
        var arrive = await client.PostAsync($"/api/transport/trips/{tripId}/arrive", null);
        Assert.Equal(HttpStatusCode.OK, arrive.StatusCode);

        await using var transport = _fixture.CreateTransportContext(tenantId);
        var assignment = await transport.TripWaybills.AsNoTracking().SingleAsync(w => w.WaybillId == waybillId);
        Assert.False(assignment.Active);
        Assert.True(await transport.TripEvents.AnyAsync(
            e => e.TripId == tripId && e.EventType == "arrived"));

        // 多段运输：到达后可装下一批次（同运单）
        var nextTrip = await client.PostAsJsonAsync("/api/transport/trips", new
        {
            originBranchId = destBranchId,                                 // 武汉
            destBranchId = originBranchId,                                 // 下一段终点（GZ01，同租户真实网点）
            vehicleId = (await transport.Vehicles.AsNoTracking().SingleAsync(v => v.PlateNo == "粤A12345")).Id,
            driverId = (Guid?)null,
            routeId = (Guid?)null,
            note = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, nextTrip.StatusCode);
        var nextTripId = (await JsonDocument.ParseAsync(await nextTrip.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("id").GetGuid();
        var reload = await client.PostAsJsonAsync($"/api/transport/trips/{nextTripId}/waybills",
            new { waybillId });
        Assert.Equal(HttpStatusCode.OK, reload.StatusCode);

        // 再次到达 → 409
        var again = await client.PostAsync($"/api/transport/trips/{tripId}/arrive", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task TripFees_AppendOnly_WithValidation()
    {
        var (tripId, _, tenantId, _, _, tenantCode) = await CreateLoadedTripAsync();
        using var client = await AdminClientAsync(tenantCode);

        var negative = await client.PostAsJsonAsync($"/api/transport/trips/{tripId}/fees",
            new { feeType = "fuel", amount = -10m, note = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);

        var unknownType = await client.PostAsJsonAsync($"/api/transport/trips/{tripId}/fees",
            new { feeType = "unknown", amount = 10m, note = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, unknownType.StatusCode);

        var ok1 = await client.PostAsJsonAsync($"/api/transport/trips/{tripId}/fees",
            new { feeType = "fuel", amount = 500m, note = "高速加油" });
        Assert.Equal(HttpStatusCode.OK, ok1.StatusCode);
        var ok2 = await client.PostAsJsonAsync($"/api/transport/trips/{tripId}/fees",
            new { feeType = "toll", amount = 120.5m, note = (string?)null });
        Assert.Equal(HttpStatusCode.OK, ok2.StatusCode);

        var list = await client.GetAsync($"/api/transport/trips/{tripId}/fees");
        var listBody = await JsonDocument.ParseAsync(await list.Content.ReadAsStreamAsync());
        Assert.Equal(2, listBody.RootElement.GetArrayLength());

        await using var platform = _fixture.CreatePlatformContext(tenantId);
        Assert.Equal(2, await platform.AuditLogs.CountAsync(a => a.Action == "transport.trip.record_fee"));
    }
}
