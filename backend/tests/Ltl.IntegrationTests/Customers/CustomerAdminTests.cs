using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Tenancy.Domain;
using Ltl.SharedKernel.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ltl.IntegrationTests.Customers;

/// <summary>CUS-002 集成测试：客户 CRUD/联系人/地址/网点授权、权限、跨租户、审计。</summary>
public class CustomerAdminTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public CustomerAdminTests(PostgreSqlFixture fixture)
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

    private async Task<(Guid TenantId, Guid BranchId, string TenantCode)> SeedAsync(params string[] permissions)
    {
        var tenantCode = $"cust-{Guid.NewGuid():N}";
        var tenant = Tenant.Create(tenantCode, "客户管理测试租户", DateTime.UtcNow);
        await using (var tenancy = _fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenant);
            await tenancy.SaveChangesAsync();
        }

        await using var org = _fixture.CreateOrganizationContext(tenant.Id);
        var branch = Branch.Create(tenant.Id, "GZ01", "广州", DateTime.UtcNow);
        var admin = User.Create(tenant.Id, "admin", "管理员", PasswordHasher.Hash("secret-123"), DateTime.UtcNow);
        var role = Role.Create(tenant.Id, "ops", "管理员", DateTime.UtcNow);
        org.Branches.Add(branch);
        org.Users.Add(admin);
        org.Roles.Add(role);
        await org.SaveChangesAsync();

        var authorization = new AuthorizationService(org);
        foreach (var permission in permissions)
        {
            await authorization.GrantPermissionAsync(role.Id, permission);
        }

        await authorization.AssignRoleAsync(admin.Id, role.Id);
        await authorization.GrantBranchScopeAsync(admin.Id, branch.Id);

        return (tenant.Id, branch.Id, tenantCode);
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

    [Fact]
    public async Task Customer_FullFlow_WithContactsAddressesAndBranchGrant()
    {
        var (tenantId, branchId, tenantCode) = await SeedAsync(
            Ltl.Modules.Customers.Domain.CustomerPermissionCodes.CustomerRead,
            Ltl.Modules.Customers.Domain.CustomerPermissionCodes.CustomerManage);
        using var client = await AdminClientAsync(tenantCode);

        var create = await client.PostAsJsonAsync("/api/customers",
            new { code = "KH001", name = "测试客户", remark = (string?)null });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync());
        var customerId = created.RootElement.GetProperty("id").GetGuid();

        var duplicate = await client.PostAsJsonAsync("/api/customers",
            new { code = "KH001", name = "重复" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var contact = await client.PostAsJsonAsync($"/api/customers/{customerId}/contacts",
            new { name = "张三", phone = "13800000000", isPrimary = true, remark = (string?)null });
        Assert.Equal(HttpStatusCode.OK, contact.StatusCode);

        var address = await client.PostAsJsonAsync($"/api/customers/{customerId}/addresses",
            new { address = "广州市白云区XX街1号", label = "仓库", isDefault = true, remark = (string?)null });
        Assert.Equal(HttpStatusCode.OK, address.StatusCode);

        var grant = await client.PostAsJsonAsync($"/api/customers/{customerId}/branches",
            new { branchId });
        Assert.Equal(HttpStatusCode.OK, grant.StatusCode);
        var grantBody = await JsonDocument.ParseAsync(await grant.Content.ReadAsStreamAsync());
        Assert.Equal(branchId, grantBody.RootElement.GetProperty("servicedBranchIds")[0].GetGuid());

        var detail = await client.GetAsync($"/api/customers/{customerId}");
        var detailBody = await JsonDocument.ParseAsync(await detail.Content.ReadAsStreamAsync());
        Assert.Equal(1, detailBody.RootElement.GetProperty("contacts").GetArrayLength());
        Assert.True(detailBody.RootElement.GetProperty("contacts")[0].GetProperty("isPrimary").GetBoolean());

        var update = await client.PutAsJsonAsync($"/api/customers/{customerId}",
            new { name = "测试客户（改）", remark = (string?)null });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var disable = await client.PostAsync($"/api/customers/{customerId}/disable", null);
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);

        var list = await client.GetAsync("/api/customers?keyword=KH001");
        var listBody = await JsonDocument.ParseAsync(await list.Content.ReadAsStreamAsync());
        Assert.Equal("Disabled", listBody.RootElement.GetProperty("items")[0].GetProperty("status").GetString());

        // 审计链
        await using var platform = _fixture.CreatePlatformContext(tenantId);
        var actions = await platform.AuditLogs
            .Where(a => a.EntityType == "customer")
            .OrderBy(a => a.OccurredAt)
            .Select(a => a.Action)
            .ToListAsync();
        Assert.Contains("customer.create", actions);
        Assert.Contains("customer.contact.add", actions);
        Assert.Contains("customer.branch.grant", actions);
        Assert.Contains("customer.disable", actions);
    }

    [Fact]
    public async Task Customer_WithoutPermission_ShouldReturn403()
    {
        var (_, _, tenantCode) = await SeedAsync(); // 无权限
        using var client = await AdminClientAsync(tenantCode);

        var response = await client.GetAsync("/api/customers");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreditPolicy_SetPersistAndClear_UpdatesCustomer()
    {
        var (tenantId, _, tenantCode) = await SeedAsync(
            Ltl.Modules.Customers.Domain.CustomerPermissionCodes.CustomerRead,
            Ltl.Modules.Customers.Domain.CustomerPermissionCodes.CustomerManage);
        using var client = await AdminClientAsync(tenantCode);

        var create = await client.PostAsJsonAsync("/api/customers", new { code = "KH900", name = "月结客户" });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var customerId = (await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("id").GetGuid();

        // 设置额度与账期
        var set = await client.PutAsJsonAsync($"/api/customers/{customerId}/credit-policy",
            new { creditLimit = 50000m, paymentTermsDays = 30 });
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        var setBody = await JsonDocument.ParseAsync(await set.Content.ReadAsStreamAsync());
        Assert.Equal(50000m, setBody.RootElement.GetProperty("creditLimit").GetDecimal());
        Assert.Equal(30, setBody.RootElement.GetProperty("paymentTermsDays").GetInt32());

        // 持久化验证：重新读取
        var get = await client.GetAsync($"/api/customers/{customerId}");
        var getBody = await JsonDocument.ParseAsync(await get.Content.ReadAsStreamAsync());
        Assert.Equal(50000m, getBody.RootElement.GetProperty("creditLimit").GetDecimal());
        Assert.Equal(30, getBody.RootElement.GetProperty("paymentTermsDays").GetInt32());

        // 负数额度被领域规则拒绝（400）
        var negative = await client.PutAsJsonAsync($"/api/customers/{customerId}/credit-policy",
            new { creditLimit = -1m, paymentTermsDays = 30 });
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);

        // 清除（null）回未设置状态
        var clear = await client.PutAsJsonAsync($"/api/customers/{customerId}/credit-policy",
            new { creditLimit = (decimal?)null, paymentTermsDays = (int?)null });
        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        var clearBody = await JsonDocument.ParseAsync(await clear.Content.ReadAsStreamAsync());
        Assert.True(clearBody.RootElement.GetProperty("creditLimit").ValueKind == JsonValueKind.Null);
        Assert.True(clearBody.RootElement.GetProperty("paymentTermsDays").ValueKind == JsonValueKind.Null);

        // 无权限写操作被拒（403）
        var (_, _, otherCode) = await SeedAsync();
        using var otherClient = await AdminClientAsync(otherCode);
        var forbidden = await otherClient.PutAsJsonAsync($"/api/customers/{customerId}/credit-policy",
            new { creditLimit = 100m, paymentTermsDays = 7 });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
}
