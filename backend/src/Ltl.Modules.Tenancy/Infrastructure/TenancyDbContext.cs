using Ltl.Modules.Tenancy.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Tenancy.Infrastructure;

/// <summary>
/// Tenancy 模块 DbContext。tenants 为全局表；
/// 后续租户范围业务表实现 ITenantScopedEntity 后，必须在此（或各模块 DbContext）配置全局查询过滤器。
/// </summary>
public sealed class TenancyDbContext(DbContextOptions<TenancyDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenancyDbContext).Assembly);
    }
}
