using System.Linq.Expressions;
using Ltl.Modules.Arrivals.Domain;
using Ltl.SharedKernel;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Arrivals.Infrastructure;

/// <summary>Arrival 模块 DbContext（ARRIVAL-001）。</summary>
public sealed class ArrivalDbContext(
    DbContextOptions<ArrivalDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    private Guid? TenantId => tenantContext.TenantId;

    internal Guid? CurrentTenantId => tenantContext.TenantId;

    public DbSet<CargoArrival> Arrivals => Set<CargoArrival>();

    public DbSet<ArrivalItem> ArrivalItems => Set<ArrivalItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ArrivalDbContext).Assembly);
        ConfigureTenantQueryFilters(modelBuilder);
    }

    private void ConfigureTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantScopedEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var body = Expression.Equal(
                Expression.Convert(
                    Expression.Property(parameter, nameof(ITenantScopedEntity.TenantId)),
                    typeof(Guid?)),
                Expression.Property(
                    Expression.Constant(this),
                    nameof(TenantId)));

            modelBuilder.Entity(entityType.ClrType)
                .HasQueryFilter(Expression.Lambda(body, parameter));
        }
    }
}
