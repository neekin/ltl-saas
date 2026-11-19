using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Tenancy.Domain;
using Ltl.SharedKernel.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Ltl.IntegrationTests.Api;

/// <summary>
/// GET /api/organization/my-branches 集成测试：网点切换器数据源（AGENTS §5.2）。
/// 覆盖租户级范围返回全部网点、受限范围仅返回授权网点、停用网点排除、未认证 401。
/// </summary>
public class MyBranchesTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public MyBranchesTests(PostgreSqlFixture fixture) => _fixture = fixture;

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

    private async Task<(Guid TenantId, Guid UserId, string TenantCode)> SeedTenantWithUserAsync()
    {
        var tenantCode = $"mybr-{Guid.NewGuid():N}";
        var tenant = Tenant.Create(tenantCode, "网点范围测试租户", DateTime.UtcNow);
        await using (var tenancy = _fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenant);
            await tenancy.SaveChangesAsync();
        }

        var user = User.Create(
            tenant.Id, "staff", "员工", PasswordHasher.Hash("secret-123"), DateTime.UtcNow);
        await using var org = _fixture.CreateOrganizationContext(tenant.Id);
        org.Users.Add(user);
        await org.SaveChangesAsync();
        return (tenant.Id, user.Id, tenantCode);
    }

    private async Task<(Guid Gz, Guid Wh, Guid Disabled)> SeedBranchesAsync(Guid tenantId)
    {
        await using var org = _fixture.CreateOrganizationContext(tenantId);
        var gz = Branch.Create(tenantId, "GZ01", "广州", DateTime.UtcNow);
        var wh = Branch.Create(tenantId, "WH02", "武汉", DateTime.UtcNow);
        var disabled = Branch.Create(tenantId, "XX09", "停用网点", DateTime.UtcNow);
        disabled.Disable(DateTime.UtcNow);
        org.Branches.AddRange(gz, wh, disabled);
        await org.SaveChangesAsync();
        return (gz.Id, wh.Id, disabled.Id);
    }

    private async Task<HttpClient> LoginAsync(string tenantCode)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { tenant_code = tenantCode, username = "staff", password = "secret-123" });
        response.EnsureSuccessStatusCode();
        var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("access_token").GetString());
        return client;
    }

    [Fact]
    public async Task TenantWideScope_ShouldReturnAllActiveBranches()
    {
        var (tenantId, userId, tenantCode) = await SeedTenantWithUserAsync();
        var (gz, wh, disabled) = await SeedBranchesAsync(tenantId);
        await using (var org = _fixture.CreateOrganizationContext(tenantId))
        {
            await new AuthorizationService(org).GrantTenantWideScopeAsync(userId);
        }

        using var client = await LoginAsync(tenantCode);
        var response = await client.GetAsync("/api/organization/my-branches");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.True(json.RootElement.GetProperty("tenantWide").GetBoolean());
        var branches = json.RootElement.GetProperty("branches");
        Assert.Equal(2, branches.GetArrayLength()); // 停用网点排除
        var codes = branches.EnumerateArray().Select(b => b.GetProperty("code").GetString()).ToArray();
        Assert.Contains("GZ01", codes);
        Assert.Contains("WH02", codes);
        Assert.DoesNotContain("XX09", codes);
    }

    [Fact]
    public async Task ScopedUser_ShouldReturnOnlyAuthorizedBranches()
    {
        var (tenantId, userId, tenantCode) = await SeedTenantWithUserAsync();
        var (gz, wh, _) = await SeedBranchesAsync(tenantId);
        await using (var org = _fixture.CreateOrganizationContext(tenantId))
        {
            var authorization = new AuthorizationService(org);
            await authorization.GrantBranchScopeAsync(userId, wh);
        }

        using var client = await LoginAsync(tenantCode);
        var response = await client.GetAsync("/api/organization/my-branches");
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        Assert.False(json.RootElement.GetProperty("tenantWide").GetBoolean());
        var branches = json.RootElement.GetProperty("branches");
        Assert.Single(branches.EnumerateArray());
        Assert.Equal("WH02", branches[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task WithoutAuthentication_ShouldReturn401()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/api/organization/my-branches");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
