using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Tenancy.Domain;
using Ltl.Modules.Transport.Domain;
using Ltl.SharedKernel.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ltl.IntegrationTests.Transport;

/// <summary>VEHICLE-001 集成测试：车辆/司机档案 CRUD、唯一性、权限。</summary>
public class FleetAdminTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public FleetAdminTests(PostgreSqlFixture fixture)
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

    private async Task<string> SeedOperatorAsync(params string[] permissions)
    {
        var tenantCode = $"fleet-{Guid.NewGuid():N}";
        var tenant = Tenant.Create(tenantCode, "运输档案测试租户", DateTime.UtcNow);
        await using (var tenancy = _fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenant);
            await tenancy.SaveChangesAsync();
        }

        await using var org = _fixture.CreateOrganizationContext(tenant.Id);
        var user = User.Create(tenant.Id, "admin", "管理员", PasswordHasher.Hash("secret-123"), DateTime.UtcNow);
        var role = Role.Create(tenant.Id, "ops", "管理员", DateTime.UtcNow);
        org.Users.Add(user);
        org.Roles.Add(role);
        await org.SaveChangesAsync();

        var authorization = new AuthorizationService(org);
        foreach (var permission in permissions)
        {
            await authorization.GrantPermissionAsync(role.Id, permission);
        }

        await authorization.AssignRoleAsync(user.Id, role.Id);
        return tenantCode;
    }

    private async Task<HttpClient> LoginAsync(string tenantCode)
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
    public async Task Vehicle_Flow_CreateDuplicateDisable_ShouldWork()
    {
        var tenantCode = await SeedOperatorAsync(
            TransportPermissionCodes.VehicleRead, TransportPermissionCodes.VehicleManage);
        using var client = await LoginAsync(tenantCode);

        var create = await client.PostAsJsonAsync("/api/transport/vehicles",
            new { plateNo = "粤A12345", vehicleType = "truck", maxLoadKg = 10000m, maxVolumeM3 = 45.5m, note = "主力干线" });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var vehicleId = (await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("id").GetString()!;

        var duplicate = await client.PostAsJsonAsync("/api/transport/vehicles",
            new { plateNo = "粤A12345", vehicleType = "van", maxLoadKg = 1000m, maxVolumeM3 = 5m });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var disable = await client.PostAsync($"/api/transport/vehicles/{vehicleId}/disable", null);
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);

        var list = await client.GetAsync("/api/transport/vehicles");
        var listBody = await JsonDocument.ParseAsync(await list.Content.ReadAsStreamAsync());
        Assert.Equal("Disabled", listBody.RootElement.GetProperty("items")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Vehicle_InvalidCapacity_ShouldReturn400()
    {
        var tenantCode = await SeedOperatorAsync(TransportPermissionCodes.VehicleManage);
        using var client = await LoginAsync(tenantCode);

        var invalid = await client.PostAsJsonAsync("/api/transport/vehicles",
            new { plateNo = "粤B00000", vehicleType = "truck", maxLoadKg = 0m, maxVolumeM3 = 5m });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Driver_Flow_WithUniqueLicense_ShouldWork()
    {
        var tenantCode = await SeedOperatorAsync(
            TransportPermissionCodes.VehicleRead, TransportPermissionCodes.VehicleManage);
        using var client = await LoginAsync(tenantCode);

        var create = await client.PostAsJsonAsync("/api/transport/drivers",
            new { name = "王师傅", phone = "13600000000", licenseNo = "LIC-001", note = (string?)null });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var duplicate = await client.PostAsJsonAsync("/api/transport/drivers",
            new { name = "李师傅", phone = "13700000000", licenseNo = "lic-001" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Fleet_WithoutPermission_ShouldReturn403()
    {
        var tenantCode = await SeedOperatorAsync(); // 无任何权限
        using var client = await LoginAsync(tenantCode);

        var response = await client.GetAsync("/api/transport/vehicles");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
