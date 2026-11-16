using System.Linq.Expressions;
using Ltl.Modules.Routing.Domain;
using Ltl.SharedKernel;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Routing.Infrastructure;

/// <summary>
/// Routing 模块 DbContext。租户范围表统一应用全局查询过滤器（约定同 ORG-001/CUS-001）：
/// 过滤器引用 DbContext 实例属性，模型缓存安全；无租户上下文 → 0 行（fail-closed）。
/// 网点属 Organization 模块数据：仅数据库层复合外键引用 branches(tenant_id, id)，不加载其实体。
/// </summary>
public sealed class RoutingDbContext(
    DbContextOptions<RoutingDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    /// <summary>当前租户 Id；查询过滤器引用本属性。</summary>
    private Guid? TenantId => tenantContext.TenantId;

    /// <summary>模块内获取当前租户 Id，用于原生 SQL 的租户范围条件（AGENTS.md §5.1）。</summary>
    internal Guid? CurrentTenantId => tenantContext.TenantId;

    public DbSet<Route> Routes => Set<Route>();

    public DbSet<RouteStop> Stops => Set<RouteStop>();

    public DbSet<TransportPlan> Plans => Set<TransportPlan>();

    public DbSet<TransportPlanSegment> PlanSegments => Set<TransportPlanSegment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RoutingDbContext).Assembly);
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
