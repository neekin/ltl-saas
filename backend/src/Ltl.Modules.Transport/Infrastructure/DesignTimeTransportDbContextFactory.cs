using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ltl.Modules.Transport.Infrastructure;

/// <summary>设计时 DbContext 工厂：供 dotnet ef 生成迁移。</summary>
public sealed class DesignTimeTransportDbContextFactory : IDesignTimeDbContextFactory<TransportDbContext>
{
    public TransportDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TransportDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=ltl_design_time;Username=ltl;Password=ltl_dev_password")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new TransportDbContext(options, new TenantContext());
    }
}
