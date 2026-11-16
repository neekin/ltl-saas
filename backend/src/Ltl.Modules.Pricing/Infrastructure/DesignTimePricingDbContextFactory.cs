using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ltl.Modules.Pricing.Infrastructure;

/// <summary>
/// 设计时 DbContext 工厂：供 dotnet ef 生成迁移。连接串仅为设计时占位，运行时由组合根注入。
/// </summary>
public sealed class DesignTimePricingDbContextFactory : IDesignTimeDbContextFactory<PricingDbContext>
{
    public PricingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PricingDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=ltl_design_time;Username=ltl;Password=ltl_dev_password")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new PricingDbContext(options, new TenantContext());
    }
}
