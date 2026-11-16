using System.Linq.Expressions;
using Ltl.Modules.Organization.Domain;
using Ltl.SharedKernel.Application;
using Ltl.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Organization.Infrastructure;

/// <summary>
/// Organization 模块 DbContext。所有租户范围表统一应用全局查询过滤器（AGENTS.md §5.1）：
/// - 上下文持有租户 Id → 只能查询该租户数据（纵深防御，非唯一安全边界）；
/// - 上下文无租户 Id → 过滤器恒为 false，返回 0 行（fail-closed）。
/// 过滤器引用 DbContext 实例属性（EF 官方多租户模式）：编译查询时按当前上下文实例求值，
/// 租户 Id 不烘焙进模型缓存——不同请求（不同租户）共享同一模型依然安全。
/// 真实授权（角色、网点范围）由服务端逐次校验，过滤器和权限校验缺一不可。
/// </summary>
public sealed class OrganizationDbContext(
    DbContextOptions<OrganizationDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    /// <summary>当前租户 Id；查询过滤器引用本属性，每次查询按当前上下文求值。</summary>
    private Guid? TenantId => tenantContext.TenantId;

    public DbSet<Branch> Branches => Set<Branch>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<UserBranchScope> UserBranchScopes => Set<UserBranchScope>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrganizationDbContext).Assembly);
        ConfigureTenantQueryFilters(modelBuilder);
    }

    /// <summary>为所有 ITenantScopedEntity 统一配置全局查询过滤器（供各模块 DbContext 复制的约定实现）。</summary>
    private void ConfigureTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantScopedEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            // e.TenantId == this.TenantId（双方转 Guid? 比较）；无租户时右值为 null → 无行返回（fail-closed）
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
