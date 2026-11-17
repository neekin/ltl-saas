using System.Linq.Expressions;
using Ltl.Modules.Finance.Domain;
using Ltl.SharedKernel;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Finance.Infrastructure;

/// <summary>Finance 模块 DbContext（FIN-001）。</summary>
public sealed class FinanceDbContext(
    DbContextOptions<FinanceDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    private Guid? TenantId => tenantContext.TenantId;
    internal Guid? CurrentTenantId => tenantContext.TenantId;

    public DbSet<Receivable> Receivables => Set<Receivable>();
    public DbSet<PaymentRecord> Payments => Set<PaymentRecord>();
    public DbSet<PaymentAllocation> Allocations => Set<PaymentAllocation>();
    public DbSet<RefundRecord> Refunds => Set<RefundRecord>();
    public DbSet<CustomerStatement> Statements => Set<CustomerStatement>();
    public DbSet<CodRefundRecord> CodRefunds => Set<CodRefundRecord>();

    // ===== COD（独立资金，AGENTS §6.5：与运费财务完全分离） =====
    public DbSet<CodOrder> CodOrders => Set<CodOrder>();
    public DbSet<CodCollection> CodCollections => Set<CodCollection>();
    public DbSet<CodSettlement> CodSettlements => Set<CodSettlement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceDbContext).Assembly);
        ConfigureTenantQueryFilters(modelBuilder);
    }

    private void ConfigureTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantScopedEntity).IsAssignableFrom(entityType.ClrType)) continue;
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var body = Expression.Equal(
                Expression.Convert(Expression.Property(parameter, nameof(ITenantScopedEntity.TenantId)), typeof(Guid?)),
                Expression.Property(Expression.Constant(this), nameof(TenantId)));
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(body, parameter));
        }
    }
}
