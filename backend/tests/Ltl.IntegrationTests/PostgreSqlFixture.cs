using Ltl.Modules.Customers.Infrastructure;
using Ltl.Modules.Organization.Infrastructure;
using Ltl.Modules.Tenancy.Infrastructure;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Ltl.IntegrationTests;

/// <summary>
/// PostgreSQL 测试容器夹具（Testcontainers）。镜像与本地 compose 一致（postgres:17-alpine）。
/// 初始化时按依赖顺序应用全部模块迁移（Tenancy → Organization），幂等。
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using (var tenancy = CreateTenancyContext())
        {
            await tenancy.Database.MigrateAsync();
        }

        await using (var organization = CreateOrganizationContext())
        {
            await organization.Database.MigrateAsync();
        }

        await using (var customers = CreateCustomersContext())
        {
            await customers.Database.MigrateAsync();
        }
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public TenancyDbContext CreateTenancyContext()
    {
        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new TenancyDbContext(options);
    }

    public OrganizationDbContext CreateOrganizationContext(Guid? tenantId = null)
    {
        var tenantContext = new TenantContext();
        if (tenantId is { } id)
        {
            tenantContext.SetTenant(id);
        }

        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseNpgsql(ConnectionStringWithDetailedErrors)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new OrganizationDbContext(options, tenantContext);
    }

    public CustomersDbContext CreateCustomersContext(Guid? tenantId = null)
    {
        var tenantContext = new TenantContext();
        if (tenantId is { } id)
        {
            tenantContext.SetTenant(id);
        }

        var options = new DbContextOptionsBuilder<CustomersDbContext>()
            .UseNpgsql(ConnectionStringWithDetailedErrors)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new CustomersDbContext(options, tenantContext);
    }

    /// <summary>测试期开放 FK 错误详情，便于失败定位。</summary>
    private string ConnectionStringWithDetailedErrors =>
        new NpgsqlConnectionStringBuilder(ConnectionString) { IncludeErrorDetail = true }.ConnectionString;
}
