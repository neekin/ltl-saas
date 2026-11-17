using Ltl.Modules.Arrivals.Infrastructure;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ltl.Modules.Arrivals.Infrastructure;

/// <summary>设计时 DbContext 工厂：供 dotnet ef 生成迁移。</summary>
public sealed class DesignTimeArrivalDbContextFactory : IDesignTimeDbContextFactory<ArrivalDbContext>
{
    public ArrivalDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ArrivalDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=ltl_design_time;Username=ltl;Password=ltl_dev_password")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ArrivalDbContext(options, new TenantContext());
    }
}
