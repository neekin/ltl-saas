using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Ltl.IntegrationTests;

/// <summary>
/// BASE-002/TEN-001 API 基线：健康检查、统一异常处理（ProblemDetails）、OpenAPI、租户上下文拒绝行为。
/// 使用 Testcontainers 提供数据库，测试自包含（CI 无需外部服务）。
/// </summary>
public class ApiBaselineTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public ApiBaselineTests(PostgreSqlFixture fixture)
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

    [Fact]
    public async Task HealthCheck_ShouldReturn200()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnhandledException_ShouldReturnProblemDetailsWithoutInternalDetails()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/dev/throw");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(body);
        Assert.Equal("服务器内部错误", body!.Title);
        // 不得泄露堆栈或异常类型（AGENTS.md §8）
        Assert.DoesNotContain("InvalidOperationException", body.Detail ?? string.Empty);
        Assert.True(body.Extensions.ContainsKey("traceId"));
    }

    [Fact]
    public async Task OpenApiDocument_ShouldBeAvailableInDevelopment()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        Assert.True(document.RootElement.TryGetProperty("paths", out _));
    }

    [Fact]
    public async Task TenancyCurrent_WithoutTenantContext_ShouldReturn401ProblemDetails()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/tenancy/current");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(body);
        Assert.Equal("未提供有效的租户上下文", body!.Title);
    }
}
