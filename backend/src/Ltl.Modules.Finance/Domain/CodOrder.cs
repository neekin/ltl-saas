namespace Ltl.Modules.Finance.Domain;

/// <summary>COD 订单状态。</summary>
public enum CodOrderStatus
{
    /// <summary>待收取。</summary>
    PendingCollection = 1,

    /// <summary>已收取（部分或全额）。</summary>
    Collected = 2,

    /// <summary>已结算（发货人应付确认）。</summary>
    Settled = 3,

    /// <summary>已付款给发货人。</summary>
    PaidOut = 4,
}

/// <summary>
/// COD 代收货款订单（cod_orders）。独立资金业务，与运费财务完全分离（AGENTS §6.5）。
/// 运单确认后懒创建（金额 = waybill.CodAmount，仅 >0 时创建）。
/// </summary>
public sealed class CodOrder : TenantScopedEntity
{
    public Guid WaybillId { get; private set; }

    /// <summary>代收金额（运单发货人委托代收的金额）。</summary>
    public decimal Amount { get; private set; }

    /// <summary>已收取金额（Σ COD 收款）。</summary>
    public decimal CollectedAmount { get; private set; }

    /// <summary>已结算金额（Σ 结算单审批通过金额）。</summary>
    public decimal SettledAmount { get; private set; }

    /// <summary>已付款金额（Σ 结算付款）。</summary>
    public decimal PaidAmount { get; private set; }

    public CodOrderStatus Status { get; private set; }

    private CodOrder()
    {
    }

    public static CodOrder Create(
        Guid tenantId, Guid waybillId, decimal amount,
        DateTime nowUtc, Guid? createdBy = null)
    {
        if (amount <= 0)
            throw new ArgumentException("COD 金额必须 >0。", nameof(amount));
        if (nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("时间必须为 UTC。", nameof(nowUtc));

        return new CodOrder
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            WaybillId = waybillId,
            Amount = amount,
            Status = CodOrderStatus.PendingCollection,
            CreatedAt = nowUtc,
            CreatedBy = createdBy,
        };
    }

    /// <summary>追加收款（不允许超额收款）。</summary>
    internal void ApplyCollection(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("COD 收款金额必须 >0。", nameof(amount));
        if (CollectedAmount + amount > Amount)
            throw new InvalidOperationException(
                $"COD 收款金额（{amount}）超出未收余额（{Amount - CollectedAmount}）。");

        CollectedAmount += amount;
        if (CollectedAmount > 0 && Status == CodOrderStatus.PendingCollection)
            Status = CodOrderStatus.Collected;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>追加结算（不允许超额结算：结算 ≤ 已收 − 已付）。</summary>
    internal void ApplySettlement(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("结算金额必须 >0。", nameof(amount));
        if (SettledAmount + amount > CollectedAmount)
            throw new InvalidOperationException(
                $"COD 结算金额（{amount}）超出可结算余额（{CollectedAmount - SettledAmount}）。");

        SettledAmount += amount;
        if (Status == CodOrderStatus.Collected)
            Status = CodOrderStatus.Settled;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>追加付款（不允许超额付款：付款 ≤ 已结算）。</summary>
    internal void ApplyPayment(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("COD 付款金额必须 >0。", nameof(amount));
        if (PaidAmount + amount > SettledAmount)
            throw new InvalidOperationException(
                $"COD 付款金额（{amount}）超出已结算余额（{SettledAmount - PaidAmount}）。");

        PaidAmount += amount;
        if (PaidAmount >= SettledAmount && SettledAmount > 0)
            Status = CodOrderStatus.PaidOut;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>退款回退已收取金额（COD 退款冲正时调用）。</summary>
    internal void ReverseCollection(decimal amount)
    {
        if (amount <= 0 || amount > CollectedAmount)
            throw new InvalidOperationException($"COD 退款金额（{amount}）超出已收取金额（{CollectedAmount}）。");
        CollectedAmount -= amount;
        if (CollectedAmount <= 0 && Status == CodOrderStatus.Collected)
            Status = CodOrderStatus.PendingCollection;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>未收余额。</summary>
    public decimal OutstandingCollection => Amount - CollectedAmount;
    /// <summary>可结算余额（已收 − 已结算）。</summary>
    public decimal AvailableForSettlement => CollectedAmount - SettledAmount;
    /// <summary>可付款余额（已结算 − 已付）。</summary>
    public decimal AvailableForPayment => SettledAmount - PaidAmount;
}
