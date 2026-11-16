using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ltl.Modules.Routing.Infrastructure;

/// <summary>
/// 设计时 DbContext 工厂：供 dotnet ef 生成迁移。连接串仅为设计时占位，运行时由组合根注入。
/// </summary>
public sealed class DesignTimeRoutingDbContextFactory : IDesignTimeDbContextFactory<RoutingDbContext>
{
    public RoutingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RoutingDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=ltl_design_time;Username=ltl;Password=ltl_dev_password")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new RoutingDbContext(options, new TenantContext());
    }
}
