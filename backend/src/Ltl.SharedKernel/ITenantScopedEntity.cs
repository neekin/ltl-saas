namespace Ltl.SharedKernel;

/// <summary>
/// 租户范围业务实体的标记接口（AGENTS.md §5.1）。
/// 实现本接口的业务表必须带 tenant_id，由所在模块的 DbContext 统一配置：
/// 1. EF Core 全局查询过滤器（纵深防御，非唯一安全边界）；
/// 2. 与租户内其他实体的引用使用复合外键 (tenant_id, entity_id)；
/// 3. 租户范围唯一约束必须包含 tenant_id。
/// </summary>
public interface ITenantScopedEntity
{
    Guid TenantId { get; }
}
