using System.Linq.Expressions;
using Ltl.Modules.Platform.Domain;
using Ltl.SharedKernel;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Platform.Infrastructure;

/// <summary>
/// Platform 模块 DbContext。branch_sequences 实现租户范围过滤器（复合主键含 tenant_id）；
/// 序号分配走原生 SQL 并显式携带租户参数，不依赖过滤器兜底（AGENTS.md §5.1）。
/// </summary>
public sealed class PlatformDbContext(
    DbContextOptions<PlatformDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    /// <summary>当前租户 Id；查询过滤器引用本属性。</summary>
    private Guid? TenantId => tenantContext.TenantId;

    /// <summary>模块内获取当前租户 Id（分配服务用于原生 SQL 参数）。</summary>
    internal Guid? CurrentTenantId => tenantContext.TenantId;

    public DbSet<BranchSequence> Sequences => Set<BranchSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlatformDbContext).Assembly);
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
