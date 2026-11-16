namespace Ltl.Modules.Warehouse.Domain;

/// <summary>
/// 收货登记（cargo_receipts，追加式，RECEIVE-001）：始发网点对已确认运单的实收记录
/// （件数、称重、体积复核）。一个运单可分批多次登记；记录不更新不删除。
/// 申报值来自运单聚合，差异在查询时计算（不固化，避免申报口径双写不一致）。
/// </summary>
public sealed class CargoReceipt : TenantScopedEntity
{
    public Guid WaybillId { get; private set; }

    /// <summary>收货网点（授权范围内）。</summary>
    public Guid BranchId { get; private set; }

    public int ReceivedPieces { get; private set; }

    /// <summary>实收称重（kg，复核值）。</summary>
    public decimal ReceivedWeightKg { get; private set; }

    /// <summary>实收体积（m³，复核值）。</summary>
    public decimal ReceivedVolumeM3 { get; private set; }

    /// <summary>操作人（审计值拷贝，不建外键）。</summary>
    public Guid? OperatorUserId { get; private set; }

    public string? Note { get; private set; }

    public DateTime OccurredAt { get; private set; }

    private CargoReceipt()
    {
    }

    public static CargoReceipt Create(
        Guid tenantId,
        Guid waybillId,
        Guid branchId,
        int receivedPieces,
        decimal receivedWeightKg,
        decimal receivedVolumeM3,
        Guid? operatorUserId,
        string? note,
        DateTime occurredAtUtc,
        Guid? createdBy = null)
    {
        if (occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("时间必须为 UTC。", nameof(occurredAtUtc));
        }

        if (receivedPieces < 1)
        {
            throw new ArgumentException("实收件数必须 ≥1。", nameof(receivedPieces));
        }

        if (receivedWeightKg < 0 || receivedVolumeM3 < 0)
        {
            throw new ArgumentException("实收重量/体积不能为负数。", nameof(receivedWeightKg));
        }

        return new CargoReceipt
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            WaybillId = waybillId,
            BranchId = branchId,
            ReceivedPieces = receivedPieces,
            ReceivedWeightKg = receivedWeightKg,
            ReceivedVolumeM3 = receivedVolumeM3,
            OperatorUserId = operatorUserId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            OccurredAt = occurredAtUtc,
            CreatedAt = occurredAtUtc,
        };
    }
}
