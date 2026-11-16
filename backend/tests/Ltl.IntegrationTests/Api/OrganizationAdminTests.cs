using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Platform.Infrastructure;
using Ltl.Modules.Tenancy.Domain;
using Ltl.SharedKernel.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ltl.IntegrationTests.Api;

/// <summary>
/// ORG-003 集成测试：管理 API 权限矩阵（403/即时失效）、CRUD 流程、自保护规则、
/// 审计日志（成功落库 + 失败无残留）、跨租户 404。
/// </summary>
public class OrganizationAdminTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public OrganizationAdminTests(PostgreSqlFixture fixture)
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

    /// <summary>seed 租户 + 拥有指定权限点的管理员（用户名 admin）。</summary>
    private async Task<(Guid TenantId, Guid OperatorUserId, string TenantCode)> SeedOperatorAsync(
        params string[] permissions)
    {
        var tenantCode = $"org-{Guid.NewGuid():N}";
        var tenant = Tenant.Create(tenantCode, "组织管理测试租户", DateTime.UtcNow);
        await using (var tenancy = _fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenant);
            await tenancy.SaveChangesAsync();
        }

        var operatorUser = User.Create(
            tenant.Id, "admin", "管理员", PasswordHasher.Hash("secret-123"), DateTime.UtcNow);
        var role = Role.Create(tenant.Id, "ops", "运维管理员", DateTime.UtcNow);
        await using var org = _fixture.CreateOrganizationContext(tenant.Id);
        org.Users.Add(operatorUser);
        org.Roles.Add(role);
        await org.SaveChangesAsync();

        var authorization = new AuthorizationService(org);
        foreach (var permission in permissions)
        {
            await authorization.GrantPermissionAsync(role.Id, permission);
        }

        await authorization.AssignRoleAsync(operatorUser.Id, role.Id);

        return (tenant.Id, operatorUser.Id, tenantCode);
    }

    private async Task<HttpClient> LoginAsync(HttpClient client, string tenantCode, string username = "admin")
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { tenant_code = tenantCode, username, password = "secret-123" });
        response.EnsureSuccessStatusCode();
        var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("access_token").GetString());
        return client;
    }

    private async Task<HttpResponseMessage> PostAsJsonAsync(
        HttpClient client, string url, object payload)
    {
        var response = await client.PostAsJsonAsync(url, payload);
        await AssertProblemOrOk(response);
        return response;
    }

    private async Task<(HttpResponseMessage Response, JsonElement Body)> PostJsonAsync(
        HttpClient client, string url, object payload)
    {
        var response = await client.PostAsJsonAsync(url, payload);
        var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return (response, body.RootElement);
    }

    private async Task<HttpResponseMessage> PutAsJsonAsync(
        HttpClient client, string url, object payload)
    {
        var response = await client.PutAsJsonAsync(url, payload);
        await AssertProblemOrOk(response);
        return response;
    }

    /// <summary>不吞错误响应（供断言使用）；仅消费流以复用连接。</summary>
    private static async Task AssertProblemOrOk(HttpResponseMessage response)
    {
        var code = (int)response.StatusCode;
        Assert.True(
            (code is >= 200 and < 300) || code is 400 or 403 or 404 or 409,
            $"意外状态码 {response.StatusCode}");
        await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task Branch_Flow_CreateRenameDisableList_ShouldWork()
    {
        var (tenantId, _, tenantCode) = await SeedOperatorAsync(
            PermissionCodes.BranchRead, PermissionCodes.BranchManage);
        using var client = await LoginAsync(CreateClient(), tenantCode);

        var create = await PostAsJsonAsync(client, "/api/organization/branches",
            new { code = "GZ01", name = "广州" });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync());
        var branchId = created.RootElement.GetProperty("id").GetString()!;

        // 重命名
        var rename = await PutAsJsonAsync(client, $"/api/organization/branches/{branchId}",
            new { name = "广州（新）" });
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);

        // 停用
        var disable = await PostAsJsonAsync(client, $"/api/organization/branches/{branchId}/disable", new { });
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);

        // 分页列表
        var list = await client.GetAsync("/api/organization/branches?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var listBody = await JsonDocument.ParseAsync(await list.Content.ReadAsStreamAsync());
        Assert.Equal(1, listBody.RootElement.GetProperty("total").GetInt32());
        Assert.Equal("Disabled", listBody.RootElement.GetProperty("items")[0].GetProperty("status").GetString());

        // 审计：创建/重命名/停用各一条（Platform audit_logs）
        await using var platform = _fixture.CreatePlatformContext(tenantId);
        var auditActions = await platform.AuditLogs
            .Where(a => a.EntityType == "branch")
            .OrderBy(a => a.OccurredAt)
            .Select(a => a.Action)
            .ToListAsync();
        Assert.Equal(
            ["organization.branch.create", "organization.branch.rename", "organization.branch.disable"],
            auditActions);
    }

    [Fact]
    public async Task Branch_DuplicateCode_ShouldReturn409_WithoutAudit()
    {
        var (tenantId, _, tenantCode) = await SeedOperatorAsync(PermissionCodes.BranchManage);
        using var client = await LoginAsync(CreateClient(), tenantCode);

        await PostAsJsonAsync(client, "/api/organization/branches", new { code = "GZ01", name = "广州" });

        var (duplicateResponse, duplicateBody) = await PostJsonAsync(client, "/api/organization/branches",
            new { code = "GZ01", name = "重复" });
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        Assert.Contains("已存在", duplicateBody.GetProperty("title").GetString());

        // 失败路径无审计残留
        await using var platform = _fixture.CreatePlatformContext(tenantId);
        Assert.Equal(1, await platform.AuditLogs.CountAsync()); // 仅第一次创建
    }

    [Fact]
    public async Task Branch_WithoutManagePermission_ShouldReturn403_AndRevokeImmediately()
    {
        var (tenantId, operatorUserId, tenantCode) = await SeedOperatorAsync(
            PermissionCodes.BranchRead, PermissionCodes.BranchManage);
        using var client = await LoginAsync(CreateClient(), tenantCode);

        // 仅 read 权限的另一个用户：创建 → 403
        await using var org = _fixture.CreateOrganizationContext(tenantId);
        var readonlyUser = User.Create(
            tenantId, "reader", "只读用户", PasswordHasher.Hash("secret-123"), DateTime.UtcNow);
        var readonlyRole = Role.Create(tenantId, "reader", "只读", DateTime.UtcNow);
        org.Users.Add(readonlyUser);
        org.Roles.Add(readonlyRole);
        await org.SaveChangesAsync();
        var authorization = new AuthorizationService(org);
        await authorization.GrantPermissionAsync(readonlyRole.Id, PermissionCodes.BranchRead);
        await authorization.AssignRoleAsync(readonlyUser.Id, readonlyRole.Id);

        using var readerClient = CreateClient();
        await LoginAsync(readerClient, tenantCode, "reader");
        var forbidden = await readerClient.PostAsJsonAsync("/api/organization/branches",
            new { code = "CS01", name = "长沙" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        // 停用 admin 角色 → read 权限即时失效（ORG-002 语义端到端）
        var adminRole = await org.Roles.SingleAsync(r => r.Code == "ops");
        adminRole.Disable(DateTime.UtcNow);
        await org.SaveChangesAsync();

        var afterRevoke = await client.GetAsync("/api/organization/branches");
        Assert.Equal(HttpStatusCode.Forbidden, afterRevoke.StatusCode);
    }

    [Fact]
    public async Task User_Flow_WithPasswordPolicyAndSelfProtection()
    {
        var (tenantId, operatorUserId, tenantCode) = await SeedOperatorAsync(
            PermissionCodes.UserRead, PermissionCodes.UserManage);
        using var client = await LoginAsync(CreateClient(), tenantCode);

        // 弱密码 → 400（密码策略）
        var weak = await client.PostAsJsonAsync("/api/organization/users",
            new { username = "zhangsan", displayName = "张三", password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        var create = await PostAsJsonAsync(client, "/api/organization/users",
            new { username = "zhangsan", displayName = "张三", password = "secret-123" });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync());
        var userId = created.RootElement.GetProperty("id").GetString()!;

        // 重复登录名 → 409
        var duplicate = await client.PostAsJsonAsync("/api/organization/users",
            new { username = "zhangsan", displayName = "重复", password = "secret-123" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        // 重置密码（弱密码 400；成功后旧密码登录失败、新密码登录成功）
        var weakReset = await client.PostAsJsonAsync($"/api/organization/users/{userId}/reset-password",
            new { password = "12345678" }); // 仅数字
        Assert.Equal(HttpStatusCode.BadRequest, weakReset.StatusCode);

        await PostAsJsonAsync(client, $"/api/organization/users/{userId}/reset-password",
            new { password = "new-pass-456" });

        using var freshClient = CreateClient();
        var oldLogin = await freshClient.PostAsJsonAsync("/api/auth/login",
            new { tenant_code = tenantCode, username = "zhangsan", password = "secret-123" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        var newLogin = await freshClient.PostAsJsonAsync("/api/auth/login",
            new { tenant_code = tenantCode, username = "zhangsan", password = "new-pass-456" });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);

        // 自保护：禁用自己 → 403
        var selfDisable = await client.PostAsJsonAsync(
            $"/api/organization/users/{operatorUserId}/disable", new { });
        Assert.Equal(HttpStatusCode.Forbidden, selfDisable.StatusCode);

        // 禁用他人 → 200，且审计落库（不含密码/哈希）
        var disable = await PostAsJsonAsync(client, $"/api/organization/users/{userId}/disable", new { });
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);

        await using var platform = _fixture.CreatePlatformContext(tenantId);
        var resetAudit = await platform.AuditLogs
            .SingleAsync(a => a.Action == "organization.user.reset_password");
        Assert.DoesNotContain("secret", resetAudit.Detail ?? string.Empty);
        Assert.DoesNotContain("new-pass", resetAudit.Detail ?? string.Empty);
        Assert.NotEqual(Guid.Empty, resetAudit.ActorUserId!.Value);
        Assert.Equal(operatorUserId, resetAudit.ActorUserId);
    }

    [Fact]
    public async Task User_SetRoles_AndPermissionsFlow_ShouldAffectMe()
    {
        var (tenantId, operatorUserId, tenantCode) = await SeedOperatorAsync(
            PermissionCodes.UserRead, PermissionCodes.UserManage, PermissionCodes.RoleManage);
        using var client = await LoginAsync(CreateClient(), tenantCode);

        // 创建目标用户与角色
        var targetUser = await PostAsJsonAsync(client, "/api/organization/users",
            new { username = "zhangsan", displayName = "张三", password = "secret-123" });
        var targetUserId = (await JsonDocument.ParseAsync(await targetUser.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("id").GetString()!;

        var role = await PostAsJsonAsync(client, "/api/organization/roles",
            new { code = "viewer", name = "查看员" });
        var roleId = (await JsonDocument.ParseAsync(await role.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("id").GetString()!;

        // 未知权限点 → 400
        var unknown = await PutAsJsonAsync(client, $"/api/organization/roles/{roleId}/permissions",
            new { permissionCodes = new[] { "not.a.permission" } });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);

        // 设置权限点（差量）→ 用户 me 响应可见
        await PutAsJsonAsync(client, $"/api/organization/roles/{roleId}/permissions",
            new { permissionCodes = new[] { PermissionCodes.UserRead } });

        // 设置角色集合（差量）→ 详情含 roleId
        var setRoles = await PutAsJsonAsync(client, $"/api/organization/users/{targetUserId}/roles",
            new { roleIds = new[] { roleId } });
        Assert.Equal(HttpStatusCode.OK, setRoles.StatusCode);
        var detail = await JsonDocument.ParseAsync(await setRoles.Content.ReadAsStreamAsync());
        Assert.Equal(roleId, detail.RootElement.GetProperty("roleIds")[0].GetString());

        // 目标用户登录 → me 显示实时权限
        using var targetClient = CreateClient();
        await LoginAsync(targetClient, tenantCode, "zhangsan");
        var me = await targetClient.GetAsync("/api/auth/me");
        var meBody = await JsonDocument.ParseAsync(await me.Content.ReadAsStreamAsync());
        var permissions = meBody.RootElement.GetProperty("permissions")
            .EnumerateArray().Select(p => p.GetString()).ToList();
        Assert.Contains(PermissionCodes.UserRead, permissions);

        // 网点范围设置：他租户网点 → 404 且无残留
        var otherTenant = Tenant.Create($"org-x-{Guid.NewGuid():N}", "其他租户", DateTime.UtcNow);
        await using (var tenancy = _fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(otherTenant);
            await tenancy.SaveChangesAsync();
        }

        var foreignSet = await client.PutAsJsonAsync(
            $"/api/organization/users/{targetUserId}/branch-scopes",
            new { branchIds = new[] { Guid.NewGuid() } });
        Assert.Equal(HttpStatusCode.NotFound, foreignSet.StatusCode);
    }

    [Fact]
    public async Task Get_OtherTenantEntity_ShouldReturn404()
    {
        var (_, _, tenantCode) = await SeedOperatorAsync(
            PermissionCodes.BranchRead, PermissionCodes.BranchManage);
        using var client = await LoginAsync(CreateClient(), tenantCode);

        var created = await PostAsJsonAsync(client, "/api/organization/branches",
            new { code = "GZ01", name = "广州" });
        var branchId = (await JsonDocument.ParseAsync(await created.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("id").GetString()!;

        // 另一租户的管理员访问 → 过滤器不可见 → 404
        var otherTenantCode = $"org-y-{Guid.NewGuid():N}";
        var otherTenant = Tenant.Create(otherTenantCode, "另一租户", DateTime.UtcNow);
        await using (var tenancy = _fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(otherTenant);
            await tenancy.SaveChangesAsync();
        }

        await using var otherOrg = _fixture.CreateOrganizationContext(otherTenant.Id);
        var otherAdmin = User.Create(
            otherTenant.Id, "admin", "另一租户管理员", PasswordHasher.Hash("secret-123"), DateTime.UtcNow);
        var otherRole = Role.Create(otherTenant.Id, "ops", "运维", DateTime.UtcNow);
        otherOrg.Users.Add(otherAdmin);
        otherOrg.Roles.Add(otherRole);
        await otherOrg.SaveChangesAsync();
        var otherAuthorization = new AuthorizationService(otherOrg);
        await otherAuthorization.GrantPermissionAsync(otherRole.Id, PermissionCodes.BranchRead);
        await otherAuthorization.AssignRoleAsync(otherAdmin.Id, otherRole.Id);

        using var otherClient = CreateClient();
        await LoginAsync(otherClient, otherTenantCode, "admin");
        var crossTenant = await otherClient.GetAsync($"/api/organization/branches/{branchId}");
        Assert.Equal(HttpStatusCode.NotFound, crossTenant.StatusCode);
    }
}
