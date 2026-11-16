namespace Ltl.Modules.Warehouse.Domain;

/// <summary>仓储作业任务类型（cargo_handling_tasks.task_type）。入库/复核等类型后续任务追加。</summary>
public enum CargoHandlingTaskType
{
    /// <summary>分拣。</summary>
    Sorting = 1,
}

/// <summary>仓储作业任务状态（cargo_handling_tasks.status）。</summary>
public enum CargoHandlingTaskStatus
{
    /// <summary>未完成。</summary>
    Pending = 1,

    /// <summary>已完成。</summary>
    Completed = 2,
}

/// <summary>
/// 仓储作业任务（cargo_handling_tasks，WAREHOUSE-001）。每运单同时最多一个未完成任务
/// （部分唯一索引强制）；完成后可因返工再次创建。任务记录追加式，不更新不删除（完成仅回填字段）。
/// </summary>
public sealed class CargoHandlingTask : TenantScopedEntity
{
    public Guid WaybillId { get; private set; }

    public Guid BranchId { get; private set; }

    public CargoHandlingTaskType TaskType { get; private set; }

    public CargoHandlingTaskStatus Status { get; private set; }

    /// <summary>创建任务的操作人（审计值拷贝）。</summary>
    public Guid? OperatorUserId { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public Guid? CompletedBy { get; private set; }

    public string? Note { get; private set; }

    private CargoHandlingTask()
    {
    }

    public static CargoHandlingTask CreateSorting(
        Guid tenantId,
        Guid waybillId,
        Guid branchId,
        Guid? operatorUserId,
        string? note,
        DateTime createdAtUtc,
        Guid? createdBy = null)
    {
        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("时间必须为 UTC。", nameof(createdAtUtc));
        }

        return new CargoHandlingTask
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            WaybillId = waybillId,
            BranchId = branchId,
            TaskType = CargoHandlingTaskType.Sorting,
            Status = CargoHandlingTaskStatus.Pending,
            OperatorUserId = operatorUserId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAt = createdAtUtc,
            CreatedBy = createdBy,
        };
    }

    /// <summary>完成任务（幂等守卫：已完成再完成 → 拒绝）。</summary>
    /// <exception cref="WarehouseDomainException">已完成时抛出（409 语义）。</exception>
    public void Complete(Guid operatorUserId, DateTime completedAtUtc)
    {
        if (Status == CargoHandlingTaskStatus.Completed)
        {
            throw new WarehouseDomainException("分拣任务已完成，不可重复完成。");
        }

        if (completedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("时间必须为 UTC。", nameof(completedAtUtc));
        }

        Status = CargoHandlingTaskStatus.Completed;
        CompletedAt = completedAtUtc;
        CompletedBy = operatorUserId;
    }
}
