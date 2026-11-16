namespace Ltl.Modules.Waybills.Domain;

/// <summary>
/// 运单事件（追加式）：关键状态变化的审计记录——租户、运单、操作网点、操作人、发生时间、
/// 前后状态、业务说明（AGENTS §6.1）。
/// branch_id/operator_id 为**审计值拷贝**（不建外键）：网点/用户只停用不物理删除，
/// 事件是历史快照，保持与主数据的生命周期解耦（与收发货人快照同理）。
/// </summary>
public sealed class WaybillEvent : TenantScopedEntity
{
    public Guid WaybillId { get; private set; }

    public WaybillEventType EventType { get; private set; }

    /// <summary>操作网点（审计值；登录交付后由服务端上下文提供）。</summary>
    public Guid? BranchId { get; private set; }

    /// <summary>操作人（审计值；认证交付后由服务端身份解析）。</summary>
    public Guid? OperatorId { get; private set; }

    /// <summary>变更前运单状态（开单事件为 null）。</summary>
    public WaybillStatus? FromStatus { get; private set; }

    /// <summary>变更后运单状态。</summary>
    public WaybillStatus ToStatus { get; private set; }

    /// <summary>发生时间（UTC）。</summary>
    public DateTime OccurredAt { get; private set; }

    /// <summary>业务说明（如取消原因、计价总额）。</summary>
    public string? Detail { get; private set; }

    private WaybillEvent()
    {
    }

    internal static WaybillEvent Create(
        Guid tenantId,
        Guid waybillId,
        WaybillEventType eventType,
        WaybillStatus? fromStatus,
        WaybillStatus toStatus,
        DateTime occurredAtUtc,
        Guid? branchId = null,
        Guid? operatorId = null,
        string? detail = null,
        Guid? createdBy = null)
    {
        if (occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("事件发生时间必须为 UTC。", nameof(occurredAtUtc));
        }

        return new WaybillEvent
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            WaybillId = waybillId,
            EventType = eventType,
            BranchId = branchId,
            OperatorId = operatorId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            OccurredAt = occurredAtUtc,
            Detail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim(),
            CreatedAt = occurredAtUtc,
            CreatedBy = createdBy,
        };
    }
}
