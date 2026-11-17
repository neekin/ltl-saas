namespace Ltl.Modules.Finance.Domain;

/// <summary>COD 结算单状态。</summary>
public enum CodSettlementStatus
{
    /// <summary>草稿（待审批）。</summary>
    Draft = 1,

    /// <summary>已审批（待付款）。</summary>
    Approved = 2,

    /// <summary>已付款。</summary>
    PaidOut = 3,
}

/// <summary>COD 结算单（cod_settlements）：发货人应付确认 → 审批 → 付款。不允许超额结算。</summary>
public sealed class CodSettlement
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CodOrderId { get; private set; }
    public decimal Amount { get; private set; }
    public CodSettlementStatus Status { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public Guid? PaidBy { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public Guid? OperatorUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private CodSettlement() { }

    public static CodSettlement Create(
        Guid tenantId, Guid codOrderId, decimal amount,
        Guid? operatorUserId, DateTime createdAtUtc)
    {
        if (amount <= 0) throw new ArgumentException("结算金额必须 >0。", nameof(amount));
        if (createdAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("时间必须为 UTC。", nameof(createdAtUtc));
        if (tenantId == Guid.Empty) throw new ArgumentException("租户 Id 不能为空 Guid。", nameof(tenantId));
        return new CodSettlement
        {
            Id = Guid.NewGuid(), TenantId = tenantId, CodOrderId = codOrderId,
            Amount = amount, Status = CodSettlementStatus.Draft,
            OperatorUserId = operatorUserId, CreatedAt = createdAtUtc,
        };
    }

    /// <summary>审批（Draft → Approved）。</summary>
    public void Approve(Guid approverId, DateTime approvedAtUtc)
    {
        if (Status != CodSettlementStatus.Draft)
            throw new InvalidOperationException("结算单已审批或已付款，不可重复审批。");
        if (approvedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("时间必须为 UTC。", nameof(approvedAtUtc));
        Status = CodSettlementStatus.Approved;
        ApprovedBy = approverId;
        ApprovedAt = approvedAtUtc;
    }

    /// <summary>付款（Approved → PaidOut；付款必须经过审批）。</summary>
    public void MarkPaidOut(Guid operatorUserId, DateTime paidAtUtc)
    {
        if (Status != CodSettlementStatus.Approved)
            throw new InvalidOperationException("结算单未审批，不可付款。");
        if (paidAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("时间必须为 UTC。", nameof(paidAtUtc));
        Status = CodSettlementStatus.PaidOut;
        PaidBy = operatorUserId;
        PaidAt = paidAtUtc;
    }
}
