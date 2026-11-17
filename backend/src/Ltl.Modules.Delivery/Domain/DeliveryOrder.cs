namespace Ltl.Modules.Delivery.Domain;

/// <summary>交付类型。</summary>
public enum DeliveryType
{
    /// <summary>自提。</summary>
    SelfPickup = 1,

    /// <summary>配送。</summary>
    Delivery = 2,
}

/// <summary>交付单状态。</summary>
public enum DeliveryOrderStatus
{
    /// <summary>待交付。</summary>
    Pending = 1,

    /// <summary>已完成（签收）。</summary>
    Completed = 2,

    /// <summary>已拒收。</summary>
    Rejected = 3,
}

/// <summary>
/// 交付单（delivery_orders）。每运单仅一张有效交付单（部分唯一索引 status IN (1)）。
/// 签收后运单交付状态 → Delivered；与运费财务状态独立（AGENTS §6.1）。
/// </summary>
public sealed class DeliveryOrder : TenantScopedEntity
{
    public Guid WaybillId { get; private set; }

    /// <summary>交付网点（目的网点）。</summary>
    public Guid BranchId { get; private set; }

    public DeliveryType DeliveryType { get; private set; }

    public DeliveryOrderStatus Status { get; private set; }

    /// <summary>配送司机（仅 Delivery 类型可选）。</summary>
    public Guid? DriverId { get; private set; }

    public string? ReceiverName { get; private set; }

    public Guid? OperatorUserId { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public Guid? CompletedBy { get; private set; }

    private DeliveryOrder()
    {
    }

    public static DeliveryOrder Create(
        Guid tenantId, Guid waybillId, Guid branchId, DeliveryType deliveryType,
        Guid? driverId, Guid? operatorUserId, DateTime createdAtUtc, Guid? createdBy = null)
    {
        if (createdAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("时间必须为 UTC。", nameof(createdAtUtc));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("租户 Id 不能为空 Guid。", nameof(tenantId));

        return new DeliveryOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WaybillId = waybillId,
            BranchId = branchId,
            DeliveryType = deliveryType,
            Status = DeliveryOrderStatus.Pending,
            DriverId = driverId,
            OperatorUserId = operatorUserId,
            CreatedAt = createdAtUtc,
            CreatedBy = createdBy,
        };
    }

    /// <summary>签收（Pending → Completed + 回填签收人）。</summary>
    public void Complete(string receiverName, DateTime completedAtUtc, Guid? completedBy)
    {
        if (Status != DeliveryOrderStatus.Pending)
            throw new InvalidOperationException("交付单已完成或拒收，不可重复签收。");
        if (completedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("时间必须为 UTC。", nameof(completedAtUtc));
        if (string.IsNullOrWhiteSpace(receiverName))
            throw new ArgumentException("签收人不能为空。", nameof(receiverName));

        Status = DeliveryOrderStatus.Completed;
        ReceiverName = receiverName.Trim();
        CompletedAt = completedAtUtc;
        CompletedBy = completedBy;
    }

    /// <summary>拒收（Pending → Rejected）。</summary>
    public void Reject(DateTime rejectedAtUtc, Guid? rejectedBy)
    {
        if (Status != DeliveryOrderStatus.Pending)
            throw new InvalidOperationException("交付单已完成或拒收，不可重复操作。");
        if (rejectedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("时间必须为 UTC。", nameof(rejectedAtUtc));

        Status = DeliveryOrderStatus.Rejected;
        CompletedAt = rejectedAtUtc;
        CompletedBy = rejectedBy;
    }
}
