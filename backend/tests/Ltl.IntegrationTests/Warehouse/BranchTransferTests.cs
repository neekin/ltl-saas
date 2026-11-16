using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Tenancy.Domain;
using Ltl.Modules.Warehouse.Domain;
using Ltl.SharedKernel.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ltl.IntegrationTests.Warehouse;

/// <summary>
/// TRANSFER-001 集成测试：调拨全流程（创建/提交/发出/验收差异）、状态守卫、
/// 组合授权（调出/目的网点范围）、验收不可覆盖、跨租户。
/// </summary>
public class BranchTransferTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public BranchTransferTests(PostgreSqlFixture fixture)
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

    /// <summary>seed：租户（GZ01/WH02）+ 调拨管理员（transfer 权限 + 两网点范围）。</summary>
    private async Task<(Guid TenantId, Guid FromBranchId, Guid ToBranchId, string TenantCode, Guid AdminId)>
        SeedAsync()
    {
        var tenantCode = $"xfer-{Guid.NewGuid():N}";
        var tenant = Tenant.Create(tenantCode, "调拨测试租户", DateTime.UtcNow);
        await using (var tenancy = _fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenant);
            await tenancy.SaveChangesAsync();
        }

        await using var org = _fixture.CreateOrganizationContext(tenant.Id);
        var from = Branch.Create(tenant.Id, "GZ01", "广州", DateTime.UtcNow);
        var to = Branch.Create(tenant.Id, "WH02", "武汉", DateTime.UtcNow);
        var admin = User.Create(tenant.Id, "admin", "管理员", PasswordHasher.Hash("secret-123"), DateTime.UtcNow);
        var role = Role.Create(tenant.Id, "ops", "管理员", DateTime.UtcNow);
        org.Branches.AddRange(from, to);
        org.Users.Add(admin);
        org.Roles.Add(role);
        await org.SaveChangesAsync();

        var authorization = new AuthorizationService(org);
        await authorization.GrantPermissionAsync(role.Id, WarehousePermissionCodes.TransferRead);
        await authorization.GrantPermissionAsync(role.Id, WarehousePermissionCodes.TransferManage);
        await authorization.AssignRoleAsync(admin.Id, role.Id);
        await authorization.GrantBranchScopeAsync(admin.Id, from.Id);
        await authorization.GrantBranchScopeAsync(admin.Id, to.Id);

        return (tenant.Id, from.Id, to.Id, tenantCode, admin.Id);
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

    private async Task<(Guid TransferId, Guid ItemId, Guid TenantId, Guid FromBranchId, Guid ToBranchId, Guid DispatchedItemId, Guid TenantId2)>
        SeedDispatchedAsync()
    {
        var (tenantId, fromBranchId, toBranchId, tenantCode, adminId) = await SeedAsync();
        using var client = await AdminClientAsync(tenantCode);

        var create = await client.PostAsJsonAsync("/api/warehouse/transfers", new
        {
            fromBranchId,
            toBranchId,
            items = new[] { new { cargoName = "纸箱", quantity = 10, remark = (string?)null } },
            note = "月度调拨",
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var transferId = (await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("id").GetGuid();

        await client.PostAsync($"/api/warehouse/transfers/{transferId}/submit", null);
        await client.PostAsync($"/api/warehouse/transfers/{transferId}/dispatch", null);

        var detail = await client.GetAsync($"/api/warehouse/transfers/{transferId}");
        var detailBody = await JsonDocument.ParseAsync(await detail.Content.ReadAsStreamAsync());
        var itemId = detailBody.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid();

        return (transferId, itemId, tenantId, fromBranchId, toBranchId, itemId, tenantId);
    }

    [Fact]
    public async Task FullFlow_WithShortageAndDamage_ShouldTrackEverything()
    {
        var (tenantId, fromBranchId, toBranchId, tenantCode, adminId) = await SeedAsync();
        using var client = await AdminClientAsync(tenantCode);

        var create = await client.PostAsJsonAsync("/api/warehouse/transfers", new
        {
            fromBranchId,
            toBranchId,
            items = new[]
            {
                new { cargoName = "纸箱", quantity = 10, remark = (string?)null },
                new { cargoName = "木架", quantity = 2, remark = (string?)null },
            },
            note = "月度调拨",
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync());
        var transferId = created.RootElement.GetProperty("id").GetGuid();
        Assert.StartsWith("DBGZ01", created.RootElement.GetProperty("transferNo").GetString());
        Assert.Equal("Draft", created.RootElement.GetProperty("status").GetString());

        // 状态守卫：未提交直接发出 → 409
        var prematureDispatch = await client.PostAsync($"/api/warehouse/transfers/{transferId}/dispatch", null);
        Assert.Equal(HttpStatusCode.Conflict, prematureDispatch.StatusCode);

        await client.PostAsync($"/api/warehouse/transfers/{transferId}/submit", null);
        await client.PostAsync($"/api/warehouse/transfers/{transferId}/dispatch", null);

        // 验收数量校验：破损 > 实收 → 409
        var detail = await client.GetAsync($"/api/warehouse/transfers/{transferId}");
        var detailBody = await JsonDocument.ParseAsync(await detail.Content.ReadAsStreamAsync());
        var items = detailBody.RootElement.GetProperty("items");
        var item1 = items[0].GetProperty("id").GetGuid();
        var item2 = items[1].GetProperty("id").GetGuid();

        var invalid = await client.PostAsJsonAsync($"/api/warehouse/transfers/{transferId}/receive", new
        {
            items = new[] { new { itemId = item1, receivedQuantity = 8, brokenQuantity = 9, remark = (string?)null } },
        });
        Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);

        // 正常验收：纸箱 10 → 实收 8（短少 2）；木架 2 → 实收 2（破损 1）
        var receive = await client.PostAsJsonAsync($"/api/warehouse/transfers/{transferId}/receive", new
        {
            items = new[]
            {
                new { itemId = item1, receivedQuantity = 8, brokenQuantity = 0, remark = "短少 2 件" },
                new { itemId = item2, receivedQuantity = 2, brokenQuantity = 1, remark = "破损 1 件" },
            },
        });
        Assert.Equal(HttpStatusCode.OK, receive.StatusCode);
        var received = await JsonDocument.ParseAsync(await receive.Content.ReadAsStreamAsync());
        Assert.Equal("Received", received.RootElement.GetProperty("status").GetString());
        Assert.Equal(8, received.RootElement.GetProperty("items")[0].GetProperty("receivedQuantity").GetInt32());
        Assert.Equal(1, received.RootElement.GetProperty("items")[1].GetProperty("brokenQuantity").GetInt32());

        // 原始交接记录不可覆盖 → 409
        var again = await client.PostAsJsonAsync($"/api/warehouse/transfers/{transferId}/receive", new
        {
            items = new[] { new { itemId = item1, receivedQuantity = 10, brokenQuantity = 0, remark = (string?)null } },
        });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        // 审计：创建/提交/发出/验收全链路
        await using var platform = _fixture.CreatePlatformContext(tenantId);
        var actions = await platform.AuditLogs
            .Where(a => a.EntityType == "branch_transfer")
            .OrderBy(a => a.OccurredAt)
            .Select(a => a.Action)
            .ToListAsync();
        Assert.Equal(
            ["warehouse.transfer.create", "warehouse.transfer.submit", "warehouse.transfer.dispatch", "warehouse.transfer.receive"],
            actions);
    }

    [Fact]
    public async Task Receive_WithoutDestinationBranchScope_ShouldReturn403()
    {
        var (tenantId, fromBranchId, toBranchId, tenantCode, adminId) = await SeedAsync();
        using var client = await AdminClientAsync(tenantCode);

        var create = await client.PostAsJsonAsync("/api/warehouse/transfers", new
        {
            fromBranchId,
            toBranchId,
            items = new[] { new { cargoName = "纸箱", quantity = 5, remark = (string?)null } },
            note = (string?)null,
        });
        var transferId = (await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("id").GetGuid();
        await client.PostAsync($"/api/warehouse/transfers/{transferId}/submit", null);
        await client.PostAsync($"/api/warehouse/transfers/{transferId}/dispatch", null);

        // 新管理员仅有调出网点范围（无目的网点范围）→ 验收 403
        await using var org = _fixture.CreateOrganizationContext(tenantId);
        var receiver = User.Create(tenantId, "receiver", "收货员", PasswordHasher.Hash("secret-123"), DateTime.UtcNow);
        var role = Role.Create(tenantId, "receiver", "收货员", DateTime.UtcNow);
        org.Users.Add(receiver);
        org.Roles.Add(role);
        await org.SaveChangesAsync();
        var authorization = new AuthorizationService(org);
        await authorization.GrantPermissionAsync(role.Id, WarehousePermissionCodes.TransferManage);
        await authorization.AssignRoleAsync(receiver.Id, role.Id);
        await authorization.GrantBranchScopeAsync(receiver.Id, fromBranchId); // 仅调出网点

        using var receiverClient = CreateClient();
        var login = await receiverClient.PostAsJsonAsync("/api/auth/login",
            new { tenant_code = tenantCode, username = "receiver", password = "secret-123" });
        login.EnsureSuccessStatusCode();
        var loginBody = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
        receiverClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", loginBody.RootElement.GetProperty("access_token").GetString());

        var detail = await client.GetAsync($"/api/warehouse/transfers/{transferId}");
        var detailBody = await JsonDocument.ParseAsync(await detail.Content.ReadAsStreamAsync());
        var itemId = detailBody.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid();

        var forbidden = await receiverClient.PostAsJsonAsync($"/api/warehouse/transfers/{transferId}/receive", new
        {
            items = new[] { new { itemId, receivedQuantity = 5, brokenQuantity = 0, remark = (string?)null } },
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Cancel_ShouldBeTerminal()
    {
        var (tenantId, fromBranchId, toBranchId, tenantCode, _) = await SeedAsync();
        using var client = await AdminClientAsync(tenantCode);

        var create = await client.PostAsJsonAsync("/api/warehouse/transfers", new
        {
            fromBranchId,
            toBranchId,
            items = new[] { new { cargoName = "纸箱", quantity = 5, remark = (string?)null } },
            note = (string?)null,
        });
        var transferId = (await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("id").GetGuid();

        var cancel = await client.PostAsync($"/api/warehouse/transfers/{transferId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var submit = await client.PostAsync($"/api/warehouse/transfers/{transferId}/submit", null);
        Assert.Equal(HttpStatusCode.Conflict, submit.StatusCode);
    }

    [Fact]
    public async Task Get_OtherTenantTransfer_ShouldReturn404()
    {
        var (tenantId, fromBranchId, toBranchId, tenantCode, _) = await SeedAsync();
        using var client = await AdminClientAsync(tenantCode);

        var create = await client.PostAsJsonAsync("/api/warehouse/transfers", new
        {
            fromBranchId,
            toBranchId,
            items = new[] { new { cargoName = "纸箱", quantity = 5, remark = (string?)null } },
            note = (string?)null,
        });
        var transferId = (await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("id").GetGuid();

        var otherTenant = Tenant.Create($"xfer-x-{Guid.NewGuid():N}", "其他租户", DateTime.UtcNow);
        await using (var tenancy = _fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(otherTenant);
            await tenancy.SaveChangesAsync();
        }

        await using var otherOrg = _fixture.CreateOrganizationContext(otherTenant.Id);
        var otherAdmin = User.Create(
            otherTenant.Id, "admin", "管理员", PasswordHasher.Hash("secret-123"), DateTime.UtcNow);
        var otherRole = Role.Create(otherTenant.Id, "ops", "管理员", DateTime.UtcNow);
        otherOrg.Users.Add(otherAdmin);
        otherOrg.Roles.Add(otherRole);
        await otherOrg.SaveChangesAsync();
        var otherAuthorization = new AuthorizationService(otherOrg);
        await otherAuthorization.GrantPermissionAsync(otherRole.Id, WarehousePermissionCodes.TransferRead);
        await otherAuthorization.AssignRoleAsync(otherAdmin.Id, otherRole.Id);

        using var otherClient = CreateClient();
        var login = await otherClient.PostAsJsonAsync("/api/auth/login",
            new { tenant_code = otherTenant.Code, username = "admin", password = "secret-123" });
        login.EnsureSuccessStatusCode();
        var loginBody = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", loginBody.RootElement.GetProperty("access_token").GetString());

        var notFound = await otherClient.GetAsync($"/api/warehouse/transfers/{transferId}");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
    }
}
