using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Tenancy.Domain;
using Ltl.SharedKernel.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Ltl.IntegrationTests.Api;

/// <summary>
/// AUTH-001 集成测试：登录（防枚举）、JWT 端到端租户解析、刷新轮换与重放拒绝、
/// 退出撤销、停用用户即时失效、FallbackPolicy 默认 401、篡改/过期 token 拒绝。
/// 使用 Testcontainers 提供数据库，测试自包含（CI 无需外部服务）。
/// </summary>
public class AuthEndpointsTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public AuthEndpointsTests(PostgreSqlFixture fixture)
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

    private async Task SeedAsync(bool grantPermission = true)
    {
        _tenantCode = $"auth-{Guid.NewGuid():N}";
        var tenant = Tenant.Create(_tenantCode, "认证测试租户", DateTime.UtcNow);
        await using (var tenancy = _fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenant);
            await tenancy.SaveChangesAsync();
        }

        _tenantId = tenant.Id;

        await using var org = _fixture.CreateOrganizationContext(_tenantId);
        var user = User.Create(
            _tenantId, "zhangsan", "张三", PasswordHasher.Hash("secret-123"), DateTime.UtcNow);
        var role = Role.Create(_tenantId, "ops", "运营管理员", DateTime.UtcNow);
        org.Users.Add(user);
        org.Roles.Add(role);
        await org.SaveChangesAsync();
        _userId = user.Id;

        if (grantPermission)
        {
            var authorization = new AuthorizationService(org);
            await authorization.GrantPermissionAsync(role.Id, PermissionCodes.UserRead);
            await authorization.AssignRoleAsync(user.Id, role.Id);
        }

        _username = "zhangsan";
    }

    private async Task<(HttpResponseMessage Response, JsonElement Body)> PostJsonAsync(
        HttpClient client, string url, object payload)
    {
        var response = await client.PostAsJsonAsync(url, payload);
        var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return (response, body.RootElement);
    }

    [Fact]
    public async Task Login_ThenCallProtectedEndpoint_ShouldResolveTenantFromClaim()
    {
        await SeedAsync();
        using var client = CreateClient();

        var (response, body) = await PostJsonAsync(client, "/api/auth/login",
            new { tenant_code = _tenantCode, username = _username, password = "secret-123" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var accessToken = body.GetProperty("access_token").GetString();
        Assert.False(string.IsNullOrEmpty(accessToken));
        Assert.True(body.GetProperty("expires_in").GetInt32() > 0);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("refresh_token").GetString()));
        Assert.Equal(_username, body.GetProperty("user").GetProperty("username").GetString());

        // 端到端：claim tenant_id → ITenantContext（ADR-0005 契约）
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var current = await client.GetAsync("/api/tenancy/current");
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        var currentBody = await JsonDocument.ParseAsync(await current.Content.ReadAsStreamAsync());
        Assert.Equal(_tenantId, Guid.Parse(currentBody.RootElement.GetProperty("tenantId").GetString()!));
    }

    [Fact]
    public async Task Login_ShouldReturnPermissionSnapshot()
    {
        await SeedAsync();
        using var client = CreateClient();

        var (_, body) = await PostJsonAsync(client, "/api/auth/login",
            new { tenant_code = _tenantCode, username = _username, password = "secret-123" });

        var permissions = body.GetProperty("user").GetProperty("permissions")
            .EnumerateArray().Select(p => p.GetString()).ToList();
        Assert.Contains(PermissionCodes.UserRead, permissions);
    }

    [Fact]
    public async Task Login_Failures_ShouldReturnUniform401()
    {
        await SeedAsync();
        using var client = CreateClient();

        var cases = new[]
        {
            new { tenant_code = _tenantCode, username = _username, password = "wrong-pass1" }, // 密码错
            new { tenant_code = _tenantCode, username = "nobody", password = "secret-123" },   // 用户不存在
            new { tenant_code = "no-such-tenant", username = _username, password = "secret-123" }, // 租户不存在
        };

        foreach (var testCase in cases)
        {
            var (response, body) = await PostJsonAsync(client, "/api/auth/login", testCase);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            // 防枚举：所有失败统一文案（AUTH-001 §2）
            Assert.Equal("登录名或密码不正确", body.GetProperty("title").GetString());
        }
    }

    [Fact]
    public async Task Refresh_ShouldRotateAndRejectReplay()
    {
        await SeedAsync();
        using var client = CreateClient();

        var (_, login) = await PostJsonAsync(client, "/api/auth/login",
            new { tenant_code = _tenantCode, username = _username, password = "secret-123" });
        var originalRefresh = login.GetProperty("refresh_token").GetString()!;

        // 第一次刷新：成功并轮换
        var (firstRefreshResponse, firstRefresh) = await PostJsonAsync(client, "/api/auth/refresh",
            new { refresh_token = originalRefresh });
        Assert.Equal(HttpStatusCode.OK, firstRefreshResponse.StatusCode);
        var newRefresh = firstRefresh.GetProperty("refresh_token").GetString()!;
        Assert.NotEqual(originalRefresh, newRefresh);
        Assert.NotEqual(login.GetProperty("access_token").GetString(),
            firstRefresh.GetProperty("access_token").GetString());

        // 重放旧令牌 → 401
        var (replayResponse, _) = await PostJsonAsync(client, "/api/auth/refresh",
            new { refresh_token = originalRefresh });
        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);

        // 新令牌可继续使用
        var (secondRefreshResponse, _) = await PostJsonAsync(client, "/api/auth/refresh",
            new { refresh_token = newRefresh });
        Assert.Equal(HttpStatusCode.OK, secondRefreshResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_ShouldRevokeRefreshToken()
    {
        await SeedAsync();
        using var client = CreateClient();

        var (_, login) = await PostJsonAsync(client, "/api/auth/login",
            new { tenant_code = _tenantCode, username = _username, password = "secret-123" });
        var refreshToken = login.GetProperty("refresh_token").GetString()!;

        var (logoutResponse, _) = await PostJsonAsync(client, "/api/auth/logout",
            new { refresh_token = refreshToken });
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        var (refreshResponse, _) = await PostJsonAsync(client, "/api/auth/refresh",
            new { refresh_token = refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);

        // 幂等：重复退出也 200
        var (reLogout, _) = await PostJsonAsync(client, "/api/auth/logout",
            new { refresh_token = refreshToken });
        Assert.Equal(HttpStatusCode.OK, reLogout.StatusCode);
    }

    [Fact]
    public async Task Refresh_AfterUserDisabled_ShouldBeRejected()
    {
        await SeedAsync(grantPermission: false);
        using var client = CreateClient();

        var (_, login) = await PostJsonAsync(client, "/api/auth/login",
            new { tenant_code = _tenantCode, username = _username, password = "secret-123" });
        var refreshToken = login.GetProperty("refresh_token").GetString()!;

        // 禁用用户（实时校验 → 刷新立即失效，ORG-002 语义）
        await using (var org = _fixture.CreateOrganizationContext(_tenantId))
        {
            var user = await org.Users.SingleAsync(u => u.Id == _userId);
            user.Disable(DateTime.UtcNow);
            await org.SaveChangesAsync();
        }

        var (refreshResponse, _) = await PostJsonAsync(client, "/api/auth/refresh",
            new { refresh_token = refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoints_WithoutOrWithBadToken_ShouldReturn401()
    {
        await SeedAsync();
        using var client = CreateClient();

        // 未认证 → FallbackPolicy 401
        var anonymous = await client.GetAsync("/api/tenancy/current");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        // 篡改签名 → 401（同一 client 按请求切换 Authorization 头；裸 HttpClient 不经过测试服务器）
        var (_, login) = await PostJsonAsync(client, "/api/auth/login",
            new { tenant_code = _tenantCode, username = _username, password = "secret-123" });
        var accessToken = login.GetProperty("access_token").GetString()!;
        var tampered = accessToken[..^4] + "xxxx";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tenancy/current")).StatusCode);

        // 过期 token（同一密钥手工签发已过期令牌）→ 401
        var expired = IssueExpiredToken(_tenantId, _userId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expired);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tenancy/current")).StatusCode);

        // 匿名白名单不受 FallbackPolicy 影响
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task Me_WithValidToken_ShouldReturnUserAndPermissions()
    {
        await SeedAsync();
        using var client = CreateClient();

        var (_, login) = await PostJsonAsync(client, "/api/auth/login",
            new { tenant_code = _tenantCode, username = _username, password = "secret-123" });
        var accessToken = login.GetProperty("access_token").GetString()!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(_userId, Guid.Parse(body.RootElement.GetProperty("userId").GetString()!));
        Assert.Equal(_tenantId, Guid.Parse(body.RootElement.GetProperty("tenantId").GetString()!));
        var permissions = body.RootElement.GetProperty("permissions")
            .EnumerateArray().Select(p => p.GetString()).ToList();
        Assert.Contains(PermissionCodes.UserRead, permissions);
    }

    /// <summary>用开发占位密钥手工签发已过期的测试令牌（过期场景）。</summary>
    private string IssueExpiredToken(Guid tenantId, Guid userId)
    {
        var secret = "dev-only-placeholder-secret-key-0123456789-change-me";
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("tenant_id", tenantId.ToString()),
        };
        var token = new JwtSecurityToken(
            "ltl-api", "ltl-clients", claims, DateTime.UtcNow.AddMinutes(-15), DateTime.UtcNow.AddMinutes(-5),
            new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // SeedAsync 填充
    private Guid _tenantId;
    private Guid _userId;
    private string _tenantCode = string.Empty;
    private string _username = string.Empty;
}
