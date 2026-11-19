using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Platform.Domain;
using Ltl.Modules.Tenancy.Domain;
using Ltl.SharedKernel.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Ltl.IntegrationTests.Api;

/// <summary>
/// NOTIFY-001 通知查询端点集成测试。
/// 锁定两个回归点：
/// 1. 分页结果以具名 record（NotificationPageResult）直接序列化，响应体必须是 { items, total }，
///    不能像旧版 C# tuple 那样序列化为空对象（commit 2ae2d39 的历史缺陷）。
/// 2. notification.read 权限校验与未认证 401。
/// </summary>
public class NotificationEndpointsTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public NotificationEndpointsTests(PostgreSqlFixture fixture) => _fixture = fixture;

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

    /// <summary>建租户 + 用户 + 角色，按 grantRead 决定是否授予 notification.read。</summary>
    private async Task<(Guid TenantId, HttpClient Client)> SeedUserAsync(bool grantRead)
    {
        var tenantCode = $"notify-{Guid.NewGuid():N}";
        var tenant = Tenant.Create(tenantCode, "通知测试租户", DateTime.UtcNow);
        await using (var tenancy = _fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenant);
            await tenancy.SaveChangesAsync();
        }

        var tenantId = tenant.Id;
        await using var org = _fixture.CreateOrganizationContext(tenantId);
        var user = User.Create(
            tenantId, "reader", "通知查阅", PasswordHasher.Hash("secret-123"), DateTime.UtcNow);
        var role = Role.Create(tenantId, "reader", "查阅角色", DateTime.UtcNow);
        org.Users.Add(user);
        org.Roles.Add(role);
        await org.SaveChangesAsync();

        var authorization = new AuthorizationService(org);
        await authorization.AssignRoleAsync(user.Id, role.Id);
        if (grantRead)
        {
            await authorization.GrantPermissionAsync(role.Id, PermissionCodes.NotificationRead);
        }

        var client = CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { tenant_code = tenantCode, username = "reader", password = "secret-123" });
        login.EnsureSuccessStatusCode();
        var body = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("access_token").GetString());
        return (tenantId, client);
    }

    private async Task SeedNotificationsAsync(Guid tenantId, int count, string eventType = "waybill.delivered")
    {
        await using var platform = _fixture.CreatePlatformContext(tenantId);
        for (var i = 0; i < count; i++)
        {
            platform.NotificationRecords.Add(NotificationRecord.Create(
                tenantId, NotificationChannel.Log, eventType, null,
                $"1380000{i:0000}", $"通知内容 {i}", DateTime.UtcNow.AddMinutes(-i)));
        }
        await platform.SaveChangesAsync();
    }

    [Fact]
    public async Task List_ShouldSerializeItemsAndTotal_WithNamedRecord()
    {
        var (tenantId, client) = await SeedUserAsync(grantRead: true);
        using (client)
        {
            await SeedNotificationsAsync(tenantId, 3);

            var response = await client.GetAsync("/api/notifications");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var root = json.RootElement;

            // 具名 record 必须序列化为 camelCase 的 items / total（回归 tuple 空对象缺陷）
            Assert.True(root.TryGetProperty("items", out var items), "响应应含 items 字段");
            Assert.True(root.TryGetProperty("total", out var total), "响应应含 total 字段");
            Assert.Equal(JsonValueKind.Array, items.ValueKind);
            Assert.Equal(3, total.GetInt32());
            Assert.Equal(3, items.GetArrayLength());

            // 查看详情字段确实被序列化（非空对象）
            var first = items[0];
            Assert.True(first.TryGetProperty("eventType", out _), "item 应含 eventType");
            Assert.True(first.TryGetProperty("content", out _), "item 应含 content");
        }
    }

    [Fact]
    public async Task List_WithPaginationAndFilter_ShouldApplyBoth()
    {
        var (tenantId, client) = await SeedUserAsync(grantRead: true);
        using (client)
        {
            await SeedNotificationsAsync(tenantId, 2, "waybill.delivered");
            await SeedNotificationsAsync(tenantId, 3, "waybill.departed");

            var paged = await client.GetAsync("/api/notifications?page=1&pageSize=2");
            var pagedJson = await JsonDocument.ParseAsync(await paged.Content.ReadAsStreamAsync());
            Assert.Equal(2, pagedJson.RootElement.GetProperty("items").GetArrayLength());
            Assert.Equal(5, pagedJson.RootElement.GetProperty("total").GetInt32());

            var filtered = await client.GetAsync("/api/notifications?eventType=waybill.departed");
            var filteredJson = await JsonDocument.ParseAsync(await filtered.Content.ReadAsStreamAsync());
            Assert.Equal(3, filteredJson.RootElement.GetProperty("total").GetInt32());
            Assert.All(
                filteredJson.RootElement.GetProperty("items").EnumerateArray(),
                item => Assert.Equal("waybill.departed", item.GetProperty("eventType").GetString()));
        }
    }

    [Fact]
    public async Task List_WithoutPermission_ShouldReturn403()
    {
        var (_, client) = await SeedUserAsync(grantRead: false);
        using (client)
        {
            var response = await client.GetAsync("/api/notifications");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task List_WithoutAuthentication_ShouldReturn401()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/api/notifications");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
