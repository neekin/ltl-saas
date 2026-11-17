using Ltl.Modules.Customers.Infrastructure;
using Ltl.Modules.Organization.Infrastructure;
using Ltl.Modules.Pricing.Infrastructure;
using Ltl.Modules.Platform.Infrastructure;
using Ltl.Modules.Routing.Infrastructure;
using Ltl.Modules.Delivery.Infrastructure;
using Ltl.Modules.Arrivals.Infrastructure;
using Ltl.Modules.Transport.Infrastructure;
using Ltl.Modules.Warehouse.Infrastructure;
using Ltl.Modules.Waybills.Infrastructure;
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

        await using (var routing = CreateRoutingContext())
        {
            await routing.Database.MigrateAsync();
        }

        await using (var platform = CreatePlatformContext())
        {
            await platform.Database.MigrateAsync();
        }

        await using (var pricing = CreatePricingContext())
        {
            await pricing.Database.MigrateAsync();
        }

        await using (var waybills = CreateWaybillsContext())
        {
            await waybills.Database.MigrateAsync();
        }

        await using (var warehouse = CreateWarehouseContext())
        {
            await warehouse.Database.MigrateAsync();
        }

        await using (var transport = CreateTransportContext())
        {
            await transport.Database.MigrateAsync();
        }

        await using (var arrival = CreateArrivalContext())
        {
            await arrival.Database.MigrateAsync();
        }

        await using (var delivery = CreateDeliveryContext())
        {
            await delivery.Database.MigrateAsync();
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

    public RoutingDbContext CreateRoutingContext(Guid? tenantId = null)
    {
        var tenantContext = new TenantContext();
        if (tenantId is { } id)
        {
            tenantContext.SetTenant(id);
        }

        var options = new DbContextOptionsBuilder<RoutingDbContext>()
            .UseNpgsql(ConnectionStringWithDetailedErrors)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new RoutingDbContext(options, tenantContext);
    }

    public PlatformDbContext CreatePlatformContext(Guid? tenantId = null)
    {
        var tenantContext = new TenantContext();
        if (tenantId is { } id)
        {
            tenantContext.SetTenant(id);
        }

        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseNpgsql(ConnectionStringWithDetailedErrors)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new PlatformDbContext(options, tenantContext);
    }

    public WaybillsDbContext CreateWaybillsContext(Guid? tenantId = null)
    {
        var tenantContext = new TenantContext();
        if (tenantId is { } id)
        {
            tenantContext.SetTenant(id);
        }

        var options = new DbContextOptionsBuilder<WaybillsDbContext>()
            .UseNpgsql(ConnectionStringWithDetailedErrors)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new WaybillsDbContext(options, tenantContext);
    }

    public WarehouseDbContext CreateWarehouseContext(Guid? tenantId = null)
    {
        var tenantContext = new TenantContext();
        if (tenantId is { } id)
        {
            tenantContext.SetTenant(id);
        }

        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseNpgsql(ConnectionStringWithDetailedErrors)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new WarehouseDbContext(options, tenantContext);
    }

    public TransportDbContext CreateTransportContext(Guid? tenantId = null)
    {
        var tenantContext = new TenantContext();
        if (tenantId is { } id)
        {
            tenantContext.SetTenant(id);
        }

        var options = new DbContextOptionsBuilder<TransportDbContext>()
            .UseNpgsql(ConnectionStringWithDetailedErrors)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new TransportDbContext(options, tenantContext);
    }

    public ArrivalDbContext CreateArrivalContext(Guid? tenantId = null)
    {
        var tenantContext = new TenantContext();
        if (tenantId is { } id)
        {
            tenantContext.SetTenant(id);
        }

        var options = new DbContextOptionsBuilder<ArrivalDbContext>()
            .UseNpgsql(ConnectionStringWithDetailedErrors)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new ArrivalDbContext(options, tenantContext);
    }

    public DeliveryDbContext CreateDeliveryContext(Guid? tenantId = null)
    {
        var tenantContext = new TenantContext();
        if (tenantId is { } id)
        {
            tenantContext.SetTenant(id);
        }

        var options = new DbContextOptionsBuilder<DeliveryDbContext>()
            .UseNpgsql(ConnectionStringWithDetailedErrors)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new DeliveryDbContext(options, tenantContext);
    }

    public PricingDbContext CreatePricingContext(Guid? tenantId = null)
    {
        var tenantContext = new TenantContext();
        if (tenantId is { } id)
        {
            tenantContext.SetTenant(id);
        }

        var options = new DbContextOptionsBuilder<PricingDbContext>()
            .UseNpgsql(ConnectionStringWithDetailedErrors)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new PricingDbContext(options, tenantContext);
    }

    /// <summary>测试期开放 FK 错误详情，便于失败定位。</summary>
    private string ConnectionStringWithDetailedErrors =>
        new NpgsqlConnectionStringBuilder(ConnectionString) { IncludeErrorDetail = true }.ConnectionString;
}
