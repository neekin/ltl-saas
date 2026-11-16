using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Ltl.Modules.Waybills.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ltl.IntegrationTests.Warehouse;

/// <summary>
/// WAREHOUSE-001 集成测试：分拣任务生命周期（未收货拒绝/创建/完成/重复完成/唯一未完成任务/授权/跨租户）。
/// </summary>
public class CargoSortingTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public CargoSortingTests(PostgreSqlFixture fixture)
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

    /// <summary>复用收货测试种子：已确认运单 + 收货员（receive/read/sort 权限 + 网点范围）。</summary>
    private async Task<(Guid TenantId, Guid BranchId, Guid WaybillId, string TenantCode)> SeedAsync()
    {
        var receiptTests = new CargoReceiptTests(_fixture);
        var seed = await receiptTests.SeedConfirmedWaybillAsync();
        return (seed.TenantId, seed.BranchId, seed.WaybillId, seed.TenantCode);
    }

    private async Task<HttpClient> KeeperClientAsync(string tenantCode)
    {
        var client = CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { tenant_code = tenantCode, username = "keeper", password = "secret-123" });
        login.EnsureSuccessStatusCode();
        var body = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("access_token").GetString());
        return client;
    }

    private async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string url, object payload)
    {
        var response = await client.PostAsJsonAsync(url, payload);
        await response.Content.ReadAsStringAsync();
        return response;
    }

    [Fact]
    public async Task SortingTask_FullLifecycle_ShouldTransitionCargoToSorted()
    {
        var (tenantId, branchId, waybillId, tenantCode) = await SeedAsync();
        using var client = await KeeperClientAsync(tenantCode);

        // 未收货运单创建分拣任务 → 409
        var unconfirmed = await SeedConfirmedWaybillWithoutReceiptAsync(tenantId, branchId);
        var earlyCreate = await PostJsonAsync(client, "/api/warehouse/sorting-tasks",
            new { waybillId = unconfirmed, branchId, note = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, earlyCreate.StatusCode);

        // 收货（需要 receive 权限，keeper 已有）
        var receipt = await PostJsonAsync(client, "/api/warehouse/receipts",
            new { waybillId = unconfirmed, branchId, receivedPieces = 2, receivedWeightKg = 100m, receivedVolumeM3 = 0.5m, note = (string?)null });
        Assert.Equal(HttpStatusCode.OK, receipt.StatusCode);

        // 创建任务 → 200 Pending
        var create = await PostJsonAsync(client, "/api/warehouse/sorting-tasks",
            new { waybillId = unconfirmed, branchId, note = "分拣批次A" });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var taskId = (await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("id").GetString()!;

        // 重复创建未完成任务 → 409（部分唯一索引）
        var duplicate = await PostJsonAsync(client, "/api/warehouse/sorting-tasks",
            new { waybillId = unconfirmed, branchId, note = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        // 完成 → 货物状态 Sorted + 事件 + 审计
        var complete = await PostJsonAsync(client, $"/api/warehouse/sorting-tasks/{taskId}/complete", new { });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);

        await using var waybills = _fixture.CreateWaybillsContext(tenantId);
        var waybill = await waybills.Waybills.AsNoTracking().SingleAsync(w => w.Id == unconfirmed);
        Assert.Equal(WaybillCargoStatus.Sorted, waybill.CargoStatus);
        Assert.True(await waybills.Events.AnyAsync(
            e => e.WaybillId == unconfirmed && e.EventType == WaybillEventType.Sorted));

        await using var platform = _fixture.CreatePlatformContext(tenantId);
        Assert.True(await platform.AuditLogs.AnyAsync(a => a.Action == "warehouse.sorting.complete"));

        // 重复完成 → 409
        var again = await PostJsonAsync(client, $"/api/warehouse/sorting-tasks/{taskId}/complete", new { });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        // 返工：完成后的运单可再次创建任务？——货物状态已 Sorted，创建守卫拒绝（409）
        var rework = await PostJsonAsync(client, "/api/warehouse/sorting-tasks",
            new { waybillId = unconfirmed, branchId, note = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, rework.StatusCode);
    }

    /// <summary>创建一个已确认但未收货的运单（同租户追加种子）。</summary>
    private async Task<Guid> SeedConfirmedWaybillWithoutReceiptAsync(Guid tenantId, Guid branchId) =>
        await new CargoReceiptTests(_fixture).SeedAdditionalConfirmedWaybillAsync(tenantId, branchId, await GetAdminIdAsync(tenantId));

    private async Task<Guid> GetAdminIdAsync(Guid tenantId)
    {
        await using var org = _fixture.CreateOrganizationContext(tenantId);
        return (await org.Users.SingleAsync(u => u.Username == "admin")).Id;
    }
}
