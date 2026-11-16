using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Tenancy.Domain;
using Ltl.Modules.Warehouse;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Ltl.Modules.Waybills.Domain;
using Ltl.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

using Ltl.IntegrationTests.Warehouse;

namespace Ltl.IntegrationTests.Waybills;

/// <summary>WB-PRINT 集成测试：打印数据完整性、waybill.read 权限、数据范围（网点/租户级）、跨租户 404。</summary>
public class WaybillPrintTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public WaybillPrintTests(PostgreSqlFixture fixture)
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

    private async Task<HttpClient> LoginAsync(string tenantCode, string username)
    {
        var client = CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { tenant_code = tenantCode, username, password = "secret-123" });
        login.EnsureSuccessStatusCode();
        var body = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("access_token").GetString());
        return client;
    }

    [Fact]
    public async Task Print_WithTenantWideScope_ShouldReturnFullView()
    {
        // 种子：管理员有组织权限但无 waybill.read、无网点范围
        var receiptTests = new CargoReceiptTests(_fixture);
        var seed = await receiptTests.SeedConfirmedWaybillAsync();

        await using var org = _fixture.CreateOrganizationContext(seed.TenantId);
        var adminRole = await org.Roles.SingleAsync(r => r.Code == "ops");
        var admin = await org.Users.SingleAsync(u => u.Username == "admin");
        var authorization = new AuthorizationService(org);
        await authorization.GrantPermissionAsync(adminRole.Id, WaybillPermissionCodes.WaybillRead);
        await authorization.GrantTenantWideScopeAsync(admin.Id);

        using var client = await LoginAsync(seed.TenantCode, "admin");
        var response = await client.GetAsync($"/api/waybills/{seed.WaybillId}/print");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("Confirmed", body.RootElement.GetProperty("waybillStatus").GetString());
        Assert.Equal(100m, body.RootElement.GetProperty("declaredWeightKg").GetDecimal());
        Assert.Equal(1, body.RootElement.GetProperty("packages").GetArrayLength());
        Assert.False(string.IsNullOrEmpty(body.RootElement.GetProperty("waybillNo").GetString()));
    }

    [Fact]
    public async Task Print_WithoutPermission_ShouldReturn403()
    {
        var receiptTests = new CargoReceiptTests(_fixture);
        var seed = await receiptTests.SeedConfirmedWaybillAsync();

        using var client = await LoginAsync(seed.TenantCode, "admin");
        var response = await client.GetAsync($"/api/waybills/{seed.WaybillId}/print");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Print_WithPermissionButNoScope_ShouldReturn403()
    {
        var receiptTests = new CargoReceiptTests(_fixture);
        var seed = await receiptTests.SeedConfirmedWaybillAsync();

        // 有 waybill.read，但无租户级范围、无网点范围
        await using var org = _fixture.CreateOrganizationContext(seed.TenantId);
        var adminRole = await org.Roles.SingleAsync(r => r.Code == "ops");
        var authorization = new AuthorizationService(org);
        await authorization.GrantPermissionAsync(adminRole.Id, WaybillPermissionCodes.WaybillRead);

        using var client = await LoginAsync(seed.TenantCode, "admin");
        var response = await client.GetAsync($"/api/waybills/{seed.WaybillId}/print");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Print_OtherTenantWaybill_ShouldReturn404()
    {
        var receiptTests = new CargoReceiptTests(_fixture);
        var seed = await receiptTests.SeedConfirmedWaybillAsync();

        var otherTenant = Tenant.Create($"print-x-{Guid.NewGuid():N}", "其他租户", DateTime.UtcNow);
        await using (var tenancy = _fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(otherTenant);
            await tenancy.SaveChangesAsync();
        }

        await using var otherOrg = _fixture.CreateOrganizationContext(otherTenant.Id);
        var otherAdmin = User.Create(
            otherTenant.Id, "admin", "另一租户管理员", PasswordHasher.Hash("secret-123"), DateTime.UtcNow);
        var otherRole = Role.Create(otherTenant.Id, "ops", "管理员", DateTime.UtcNow);
        otherOrg.Users.Add(otherAdmin);
        otherOrg.Roles.Add(otherRole);
        await otherOrg.SaveChangesAsync();
        var otherAuthorization = new AuthorizationService(otherOrg);
        await otherAuthorization.GrantPermissionAsync(otherRole.Id, WaybillPermissionCodes.WaybillRead);
        await otherAuthorization.GrantTenantWideScopeAsync(otherAdmin.Id);

        using var otherClient = await LoginAsync(otherTenant.Code, "admin");
        var response = await otherClient.GetAsync($"/api/waybills/{seed.WaybillId}/print");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
