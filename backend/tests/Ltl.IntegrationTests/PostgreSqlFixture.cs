using Testcontainers.PostgreSql;
using Xunit;

namespace Ltl.IntegrationTests;

/// <summary>
/// PostgreSQL 测试容器夹具（Testcontainers）。镜像与本地 compose 一致（postgres:17-alpine）。
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
