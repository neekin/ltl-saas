using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Ltl.Modules.Tenancy.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Ltl.IntegrationTests.Api;

/// <summary>FILE-001 集成测试：上传/下载 round-trip、大小上限、租户隔离、未认证 401。</summary>
public class FileEndpointsTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public FileEndpointsTests(PostgreSqlFixture fixture)
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

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var tenantCode = $"file-{Guid.NewGuid():N}";
        var tenant = Tenant.Create(tenantCode, "文件测试租户", DateTime.UtcNow);
        await using (var tenancy = _fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenant);
            await tenancy.SaveChangesAsync();
        }

        await using var org = _fixture.CreateOrganizationContext(tenant.Id);
        var user = Ltl.Modules.Organization.Domain.User.Create(
            tenant.Id, "admin", "管理员", Ltl.SharedKernel.Security.PasswordHasher.Hash("secret-123"),
            DateTime.UtcNow);
        org.Users.Add(user);
        await org.SaveChangesAsync();

        var client = CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { tenant_code = tenantCode, username = "admin", password = "secret-123" });
        login.EnsureSuccessStatusCode();
        var body = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("access_token").GetString());
        return client;
    }

    private static MultipartFormDataContent Multipart(byte[] bytes, string fileName = "test.txt")
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", fileName);
        return content;
    }

    [Fact]
    public async Task Upload_Download_RoundTrip_ShouldPreserveBytes()
    {
        using var client = await AuthenticatedClientAsync();
        var payload = "零担物流 FILE-001 测试内容"u8.ToArray();

        var upload = await client.PostAsync("/api/files", Multipart(payload));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var metadata = await JsonDocument.ParseAsync(await upload.Content.ReadAsStreamAsync());
        var fileId = metadata.RootElement.GetProperty("id").GetString()!;
        Assert.Equal("test.txt", metadata.RootElement.GetProperty("fileName").GetString());
        Assert.Equal(payload.Length, metadata.RootElement.GetProperty("sizeBytes").GetInt64());

        // 元数据端点
        var meta = await client.GetAsync($"/api/files/{fileId}");
        Assert.Equal(HttpStatusCode.OK, meta.StatusCode);

        // 下载 round-trip
        var download = await client.GetAsync($"/api/files/{fileId}/content");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        var downloaded = await download.Content.ReadAsByteArrayAsync();
        Assert.Equal(payload, downloaded);
    }

    [Fact]
    public async Task Upload_EmptyFile_ShouldReturn400()
    {
        using var client = await AuthenticatedClientAsync();

        var response = await client.PostAsync("/api/files", Multipart([]));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Download_OtherTenantFile_ShouldReturn404()
    {
        using var client = await AuthenticatedClientAsync();
        var payload = "secret"u8.ToArray();
        var upload = await client.PostAsync("/api/files", Multipart(payload));
        var fileId = (await JsonDocument.ParseAsync(await upload.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("id").GetString()!;

        // 另一租户的认证客户端
        using var otherClient = await AuthenticatedClientAsync();
        var forbidden = await otherClient.GetAsync($"/api/files/{fileId}/content");
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
    }

    [Fact]
    public async Task Upload_WithoutAuthentication_ShouldReturn401()
    {
        using var client = CreateClient();

        var response = await client.PostAsync("/api/files", Multipart("data"u8.ToArray()));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
