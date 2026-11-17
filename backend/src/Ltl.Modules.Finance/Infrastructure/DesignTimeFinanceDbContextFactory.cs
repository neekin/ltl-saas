using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ltl.Modules.Finance.Infrastructure;

public sealed class DesignTimeFinanceDbContextFactory : IDesignTimeDbContextFactory<FinanceDbContext>
{
    public FinanceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=ltl_design_time;Username=ltl;Password=ltl_dev_password")
            .UseSnakeCaseNamingConvention()
            .Options;
        return new FinanceDbContext(options, new TenantContext());
    }
}
