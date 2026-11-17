using System.Linq.Expressions;
using Ltl.Modules.Delivery.Domain;
using Ltl.SharedKernel;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Delivery.Infrastructure;

/// <summary>Delivery 模块 DbContext（DELIVERY-001）。</summary>
public sealed class DeliveryDbContext(
    DbContextOptions<DeliveryDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    private Guid? TenantId => tenantContext.TenantId;

    internal Guid? CurrentTenantId => tenantContext.TenantId;

    public DbSet<DeliveryOrder> Orders => Set<DeliveryOrder>();
    public DbSet<ProofOfDelivery> Proofs => Set<ProofOfDelivery>();
    public DbSet<DeliveryEvent> Events => Set<DeliveryEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DeliveryDbContext).Assembly);
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
