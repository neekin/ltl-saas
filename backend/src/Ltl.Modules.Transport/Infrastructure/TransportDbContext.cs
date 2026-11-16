using System.Linq.Expressions;
using Ltl.Modules.Transport.Domain;
using Ltl.SharedKernel;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Transport.Infrastructure;

/// <summary>Transport 模块 DbContext（VEHICLE-001 起步，TRIP-001 扩展）。</summary>
public sealed class TransportDbContext(
    DbContextOptions<TransportDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    private Guid? TenantId => tenantContext.TenantId;

    /// <summary>模块内服务获取当前租户 Id（同其他模块 CurrentTenantId 约定）。</summary>
    internal Guid? CurrentTenantId => tenantContext.TenantId;

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<Driver> Drivers => Set<Driver>();

    public DbSet<TransportTrip> Trips => Set<TransportTrip>();

    public DbSet<TransportTripSegment> TripSegments => Set<TransportTripSegment>();

    public DbSet<TransportTripWaybill> TripWaybills => Set<TransportTripWaybill>();

    public DbSet<TransportTripFee> TripFees => Set<TransportTripFee>();

    public DbSet<TransportTripEvent> TripEvents => Set<TransportTripEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TransportDbContext).Assembly);
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
