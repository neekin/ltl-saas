using Ltl.Modules.Tenancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ltl.Modules.Tenancy.Infrastructure;

/// <summary>
/// 设计时 DbContext 工厂：供 dotnet ef 生成迁移。
/// 连接串仅为设计时模型读取占位（不会被运行时使用）；运行时连接串由组合根按环境注入。
/// </summary>
public sealed class DesignTimeTenancyDbContextFactory : IDesignTimeDbContextFactory<TenancyDbContext>
{
    public TenancyDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=ltl_design_time;Username=ltl;Password=ltl_dev_password")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new TenancyDbContext(options);
    }
}
