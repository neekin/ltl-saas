using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Waybills.Domain;
using Ltl.SharedKernel.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ltl.IntegrationTests.Waybills;

/// <summary>
/// WB-003 集成测试：运单列表/详情、草稿修改（整体替换/确认后 409）、确认/取消端点、事件链。
/// </summary>
public class WaybillAdminTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public WaybillAdminTests(PostgreSqlFixture fixture)
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

    /// <summary>复用运输种子：租户（GZ01/WH02/客户/价卡/3 张已确认运单）+ waybill 权限 + 租户级范围。</summary>
    private async Task<TripSeedData> SeedAsync()
    {
        var loadTests = new Ltl.IntegrationTests.Transport.TripLoadTests(_fixture);
        var seed = await loadTests.SeedAsyncPublic();

        await using var org = _fixture.CreateOrganizationContext(seed.TenantId);
        var admin = await org.Users.SingleAsync(u => u.Username == "admin");
        var role = await org.Roles.SingleAsync(r => r.Code == "ops");
        var authorization = new AuthorizationService(org);
        await authorization.GrantPermissionAsync(role.Id, WaybillPermissionCodes.WaybillRead);
        await authorization.GrantPermissionAsync(role.Id, WaybillPermissionCodes.WaybillManage);
        await authorization.GrantTenantWideScopeAsync(admin.Id);

        await using var org2 = _fixture.CreateOrganizationContext(seed.TenantId);
        var dest = await org2.Branches.SingleAsync(b => b.Code == "WH02");

        await using var customers = _fixture.CreateCustomersContext(seed.TenantId);
        var customer = await customers.Customers.SingleAsync(c => c.Code == "KH001");

        await using var pricing = _fixture.CreatePricingContext(seed.TenantId);
        var ruleSet = await pricing.RuleSets.SingleAsync(s => s.Code == "std");

        return new TripSeedData(
            seed.TenantId, seed.OriginBranchId, dest.Id, customer.Id, ruleSet.Id,
            seed.WaybillIds, seed.TenantCode, admin.Id);
    }

    public sealed record TripSeedData(
        Guid TenantId, Guid OriginBranchId, Guid DestBranchId, Guid CustomerId, Guid RuleSetId,
        IReadOnlyList<Guid> WaybillIds, string TenantCode, Guid AdminId);

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

    private async Task<Ltl.Modules.Waybills.Application.WaybillService> WaybillServiceAsync(Guid tenantId) =>
        new(
            _fixture.CreateWaybillsContext(tenantId),
            new Ltl.Modules.Platform.Application.BranchSequenceService(_fixture.CreatePlatformContext(tenantId)),
            new Ltl.Modules.Pricing.Application.PricingTrialService(_fixture.CreatePricingContext(tenantId)),
            new Ltl.Modules.Pricing.Application.PricingSnapshotSerializer());

    private async Task<Ltl.Modules.Waybills.Application.CreateWaybillCommand> DraftCommandAsync(
        TripSeedData seed, Guid operatorId) =>
        new(seed.OriginBranchId, seed.DestBranchId, seed.CustomerId, WaybillPaymentType.Cash,
            "旧发货人", "138", "旧地址", "旧收货人", "139", "旧地址2",
            [new WaybillPackageDraft("旧货物", 1, 10m, 0.1m)],
            seed.RuleSetId, DateOnly.FromDateTime(DateTime.UtcNow), OperatorId: operatorId);

    [Fact]
    public async Task List_And_Detail_ShouldReturnWaybills()
    {
        var seed = await SeedAsync();
        using var client = await AdminClientAsync(seed.TenantCode);

        var list = await client.GetAsync("/api/waybills?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var listBody = await JsonDocument.ParseAsync(await list.Content.ReadAsStreamAsync());
        Assert.Equal(3, listBody.RootElement.GetProperty("total").GetInt32());

        var detail = await client.GetAsync($"/api/waybills/{seed.WaybillIds[0]}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var detailBody = await JsonDocument.ParseAsync(await detail.Content.ReadAsStreamAsync());
        Assert.Equal(1, detailBody.RootElement.GetProperty("packages").GetArrayLength());
        Assert.Equal("Confirmed", detailBody.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task UpdateDraft_ShouldReplaceSnapshotAndPackages_ConfirmedRejected()
    {
        var seed = await SeedAsync();
        using var client = await AdminClientAsync(seed.TenantCode);

        // 已确认运单修改 → 409（聚合守卫）
        var confirmedUpdate = await client.PutAsJsonAsync($"/api/waybills/{seed.WaybillIds[0]}/draft", new
        {
            senderName = "新发货人", senderPhone = "1", senderAddress = "A",
            receiverName = "新收货人", receiverPhone = "2", receiverAddress = "B",
            remark = (string?)null,
            packages = new[] { new { cargoName = "新货", quantity = 3, weightKg = 30m, volumeM3 = 0.3m } },
        });
        Assert.Equal(HttpStatusCode.Conflict, confirmedUpdate.StatusCode);

        // 草稿修改：整体替换
        var waybillsService = await WaybillServiceAsync(seed.TenantId);
        var draft = await waybillsService.CreateWaybillAsync(await DraftCommandAsync(seed, seed.AdminId));

        var update = await client.PutAsJsonAsync($"/api/waybills/{draft.WaybillId}/draft", new
        {
            senderName = "新发货人", senderPhone = "138", senderAddress = "新地址",
            receiverName = "新收货人", receiverPhone = "139", receiverAddress = "新地址2",
            remark = "改好了",
            packages = new[]
            {
                new { cargoName = "新货物A", quantity = 2, weightKg = 20m, volumeM3 = 0.2m },
                new { cargoName = "新货物B", quantity = 1, weightKg = 5m, volumeM3 = 0.05m },
            },
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await JsonDocument.ParseAsync(await update.Content.ReadAsStreamAsync());
        Assert.Equal("新发货人", updated.RootElement.GetProperty("senderName").GetString());
        Assert.Equal(2, updated.RootElement.GetProperty("packages").GetArrayLength());

        await using var verification = _fixture.CreateWaybillsContext(seed.TenantId);
        var reloaded = await verification.Waybills.AsNoTracking()
            .Include(w => w.Packages)
            .SingleAsync(w => w.Id == draft.WaybillId);
        Assert.Equal(2, reloaded.Packages.Count);
        Assert.Equal([1, 2], reloaded.Packages.Select(p => p.PackageNo));
    }

    [Fact]
    public async Task Confirm_And_Cancel_ViaApi_ShouldRecordEvents()
    {
        var seed = await SeedAsync();
        var waybillsService = await WaybillServiceAsync(seed.TenantId);
        var draft = await waybillsService.CreateWaybillAsync(await DraftCommandAsync(seed, seed.AdminId));

        using var client = await AdminClientAsync(seed.TenantCode);
        var confirm = await client.PostAsync($"/api/waybills/{draft.WaybillId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var cancel = await client.PostAsJsonAsync($"/api/waybills/{draft.WaybillId}/cancel",
            new { reason = "客户取消" });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        await using var verification = _fixture.CreateWaybillsContext(seed.TenantId);
        var waybill = await verification.Waybills.AsNoTracking().SingleAsync(w => w.Id == draft.WaybillId);
        Assert.Equal(WaybillStatus.Cancelled, waybill.Status);
        var events = await verification.Events.AsNoTracking()
            .Where(e => e.WaybillId == draft.WaybillId)
            .OrderBy(e => e.OccurredAt)
            .Select(e => e.EventType)
            .ToListAsync();
        Assert.Equal(
            [WaybillEventType.Created, WaybillEventType.Confirmed, WaybillEventType.Cancelled],
            events);
        Assert.Contains("客户取消", (await verification.Events.AsNoTracking()
            .SingleAsync(e => e.WaybillId == draft.WaybillId && e.EventType == WaybillEventType.Cancelled)).Detail);
    }
    [Fact]
    public async Task CreateWaybill_WithIdempotencyKeyHeader_ShouldReturnSameWaybill()
    {
        var seed = await SeedAsync();
        using var client = await AdminClientAsync(seed.TenantCode);
        var idempotencyKey = Guid.NewGuid();

        var request = new
        {
            originBranchId = seed.OriginBranchId,
            destBranchId = seed.DestBranchId,
            customerId = seed.CustomerId,
            ruleSetId = seed.RuleSetId,
            paymentType = "Cash",
            senderName = "张三", senderPhone = "138", senderAddress = "广州",
            receiverName = "李四", receiverPhone = "139", receiverAddress = "武汉",
            packages = new object[] { new { cargoName = "纸箱", quantity = 1, weightKg = 10m, volumeM3 = 0.1m } },
        };

        client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey.ToString());
        var first = await client.PostAsJsonAsync("/api/waybills", request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await JsonDocument.ParseAsync(await first.Content.ReadAsStreamAsync());
        var waybillId = firstBody.RootElement.GetProperty("waybillId").GetGuid();

        // 同键重复提交（网络重试/双击）→ 返回原单，不产生第二条
        var second = await client.PostAsJsonAsync("/api/waybills", request);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await JsonDocument.ParseAsync(await second.Content.ReadAsStreamAsync());
        Assert.Equal(waybillId, secondBody.RootElement.GetProperty("waybillId").GetGuid());

        await using var verification = _fixture.CreateWaybillsContext(seed.TenantId);
        Assert.Equal(1, await verification.Waybills.CountAsync(w => w.IdempotencyKey == idempotencyKey));
    }

    [Fact]
    public async Task CreateWaybill_WithoutPermissionOrBranchScope_ShouldReturn403_AndListScopedToBranch()
    {
        var seed = await SeedAsync();

        // 新增第三网点 CS03 与仅限 CS03 的只读/开单用户
        await using (var org = _fixture.CreateOrganizationContext(seed.TenantId))
        {
            var thirdBranch = Branch.Create(seed.TenantId, "CS03", "长沙", DateTime.UtcNow);
            var reader = User.Create(seed.TenantId, "reader", "只读员", PasswordHasher.Hash("secret-123"), DateTime.UtcNow);
            var branchOps = User.Create(seed.TenantId, "csops", "长沙开单员", PasswordHasher.Hash("secret-123"), DateTime.UtcNow);
            var readRole = Role.Create(seed.TenantId, "wb-read", "运单只读", DateTime.UtcNow);
            var manageRole = Role.Create(seed.TenantId, "wb-manage", "运单管理", DateTime.UtcNow);
            org.Branches.Add(thirdBranch);
            org.Users.AddRange(reader, branchOps);
            org.Roles.AddRange(readRole, manageRole);
            await org.SaveChangesAsync();

            var auth = new AuthorizationService(org);
            await auth.GrantPermissionAsync(readRole.Id, WaybillPermissionCodes.WaybillRead);
            await auth.AssignRoleAsync(reader.Id, readRole.Id);
            await auth.GrantBranchScopeAsync(reader.Id, thirdBranch.Id);

            await auth.GrantPermissionAsync(manageRole.Id, WaybillPermissionCodes.WaybillRead);
            await auth.GrantPermissionAsync(manageRole.Id, WaybillPermissionCodes.WaybillManage);
            await auth.AssignRoleAsync(branchOps.Id, manageRole.Id);
            await auth.GrantBranchScopeAsync(branchOps.Id, thirdBranch.Id);
        }

        var request = new
        {
            originBranchId = seed.OriginBranchId,
            destBranchId = seed.DestBranchId,
            customerId = seed.CustomerId,
            ruleSetId = seed.RuleSetId,
            paymentType = "Cash",
            senderName = "张三", senderPhone = "138", senderAddress = "广州",
            receiverName = "李四", receiverPhone = "139", receiverAddress = "武汉",
            packages = new object[] { new { cargoName = "纸箱", quantity = 1, weightKg = 10m, volumeM3 = 0.1m } },
        };

        // 1) 无 waybill.manage 权限 → POST /api/waybills 403
        using var readerClient = CreateClient();
        var readerLogin = await readerClient.PostAsJsonAsync("/api/auth/login",
            new { tenant_code = seed.TenantCode, username = "reader", password = "secret-123" });
        readerLogin.EnsureSuccessStatusCode();
        var readerToken = (await JsonDocument.ParseAsync(await readerLogin.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("access_token").GetString();
        readerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", readerToken);

        var noPermCreate = await readerClient.PostAsJsonAsync("/api/waybills", request);
        Assert.Equal(HttpStatusCode.Forbidden, noPermCreate.StatusCode);

        // 2) 仅授权 CS03 网点的用户查列表 → 看不到 GZ01→WH02 的 3 张运单
        var scopedList = await readerClient.GetAsync("/api/waybills?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, scopedList.StatusCode);
        var scopedListBody = await JsonDocument.ParseAsync(await scopedList.Content.ReadAsStreamAsync());
        Assert.Equal(0, scopedListBody.RootElement.GetProperty("total").GetInt32());

        // 3) 有 waybill.manage 但仅授权 CS03 网点 → 为 GZ01 开单 403
        using var csOpsClient = CreateClient();
        var opsLogin = await csOpsClient.PostAsJsonAsync("/api/auth/login",
            new { tenant_code = seed.TenantCode, username = "csops", password = "secret-123" });
        opsLogin.EnsureSuccessStatusCode();
        var opsToken = (await JsonDocument.ParseAsync(await opsLogin.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("access_token").GetString();
        csOpsClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opsToken);

        var wrongBranchCreate = await csOpsClient.PostAsJsonAsync("/api/waybills", request);
        Assert.Equal(HttpStatusCode.Forbidden, wrongBranchCreate.StatusCode);
    }
}
