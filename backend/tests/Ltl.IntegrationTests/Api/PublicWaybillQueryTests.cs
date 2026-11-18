using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Ltl.IntegrationTests.Api;

/// <summary>
/// P10 匿名客户查单端点契约：畸形请求（空 body / 空字段 / 缺字段）统一 401，
/// 不得因 null 入参解引用产生 500 或泄露内部异常（AGENTS §8 防枚举、不暴露细节）。
/// </summary>
public class PublicWaybillQueryTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public PublicWaybillQueryTests(PostgreSqlFixture fixture)
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

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"tenantCode\":\"\",\"waybillNo\":\"\",\"queryCode\":\"\"}")]
    [InlineData("{\"tenantCode\":\"demo\"}")]
    [InlineData("{\"tenantCode\":\"demo\",\"waybillNo\":\"WB001\"}")]
    [InlineData("{\"tenantCode\":null,\"waybillNo\":null,\"queryCode\":null}")]
    public async Task MalformedBody_ShouldReturn401_NotServerError(string json)
    {
        using var client = CreateClient();

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/public/waybill-query", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NoBody_ShouldReturn401_NotServerError()
    {
        using var client = CreateClient();

        var response = await client.PostAsync("/api/public/waybill-query", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WellFormedButWrongCredentials_ShouldReturn401()
    {
        using var client = CreateClient();

        using var content = new StringContent(
            "{\"tenantCode\":\"not-exist\",\"waybillNo\":\"WB001\",\"queryCode\":\"000000\"}",
            Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/public/waybill-query", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
