using Ltl.SharedKernel;

namespace Ltl.Modules.Platform.Domain;

/// <summary>
/// 审计日志（audit_logs，追加式，ORG-003）：关键写操作的留痕——谁（actor_user_id，审计值拷贝）、
/// 何时、对什么实体（entity_type/entity_id）、做了什么（action）、摘要（detail jsonb）。
/// detail 不得包含密码哈希/令牌等敏感值（AGENTS §11）。不更新、不删除。
/// </summary>
public sealed class AuditLog : ITenantScopedEntity
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public DateTime OccurredAt { get; private set; }

    public Guid? ActorUserId { get; private set; }

    /// <summary>动作编码（如 organization.branch.create）。</summary>
    public string Action { get; private set; } = null!;

    public string EntityType { get; private set; } = null!;

    public Guid? EntityId { get; private set; }

    /// <summary>操作摘要 JSONB。</summary>
    public string? Detail { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private AuditLog()
    {
    }

    public static AuditLog Create(
        Guid tenantId,
        string action,
        string entityType,
        Guid? entityId,
        Guid? actorUserId,
        string? detailJson,
        DateTime occurredAtUtc)
    {
        if (occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("时间必须为 UTC。", nameof(occurredAtUtc));
        }

        if (string.IsNullOrWhiteSpace(action) || action.Length > 100)
        {
            throw new ArgumentException("审计动作编码不能为空且长度 ≤100。", nameof(action));
        }

        if (string.IsNullOrWhiteSpace(entityType) || entityType.Length > 64)
        {
            throw new ArgumentException("审计实体类型不能为空且长度 ≤64。", nameof(entityType));
        }

        return new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            ActorUserId = actorUserId,
            Detail = detailJson,
            CreatedAt = occurredAtUtc,
        };
    }
}
