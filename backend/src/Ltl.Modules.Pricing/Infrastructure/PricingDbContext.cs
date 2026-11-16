using System.Linq.Expressions;
using Ltl.Modules.Pricing.Domain;
using Ltl.SharedKernel;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Pricing.Infrastructure;

/// <summary>
/// Pricing 模块 DbContext。规则集/版本均为租户范围实体（复合外键 + 全局过滤器约定）；
/// 已发布定义不可变由领域守卫 + 测试守护，数据库层不设触发器（与现有模块一致）。
/// </summary>
public sealed class PricingDbContext(
    DbContextOptions<PricingDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    /// <summary>当前租户 Id；查询过滤器引用本属性（EF 官方多租户模式，防模型缓存烘焙）。</summary>
    private Guid? TenantId => tenantContext.TenantId;

    /// <summary>模块内服务获取当前租户 Id（同 PlatformDbContext.CurrentTenantId 约定）。</summary>
    internal Guid? CurrentTenantId => tenantContext.TenantId;

    public DbSet<PricingRuleSet> RuleSets => Set<PricingRuleSet>();

    public DbSet<PricingRuleSetVersion> RuleSetVersions => Set<PricingRuleSetVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PricingDbContext).Assembly);
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
