namespace Ltl.Modules.Finance.Domain;

/// <summary>应收状态。</summary>
public enum ReceivableStatus
{
    /// <summary>未收款。</summary>
    Unpaid = 1,

    /// <summary>部分收款。</summary>
    PartiallyPaid = 2,

    /// <summary>已结清。</summary>
    Settled = 3,
}

/// <summary>
/// 应收账款（receivables）：运单确认时自动创建（金额 = 运单 TotalAmount）。
/// settled_amount 由核销驱动（追加式收款 → 核销 → 更新累计）；一个运单一张应收。
/// </summary>
public sealed class Receivable : TenantScopedEntity
{
    public Guid WaybillId { get; private set; }

    /// <summary>应收金额（= 运单固化总费用）。</summary>
    public decimal Amount { get; private set; }

    /// <summary>已核销金额（Σ收款核销）。</summary>
    public decimal SettledAmount { get; private set; }

    public ReceivableStatus Status { get; private set; }

    private Receivable()
    {
    }

    public static Receivable Create(
        Guid tenantId, Guid waybillId, decimal amount,
        DateTime nowUtc, Guid? createdBy = null)
    {
        if (amount <= 0)
            throw new ArgumentException("应收金额必须 >0。", nameof(amount));
        if (nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("时间必须为 UTC。", nameof(nowUtc));

        return new Receivable
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            WaybillId = waybillId,
            Amount = amount,
            SettledAmount = 0,
            Status = ReceivableStatus.Unpaid,
            CreatedAt = nowUtc,
            CreatedBy = createdBy,
        };
    }

    /// <summary>核销收款（由 FinanceService 在环境事务内调用）。</summary>
    internal void ApplySettlement(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("核销金额必须 >0。", nameof(amount));
        if (SettledAmount + amount > Amount)
            throw new InvalidOperationException(
                $"核销金额（{amount}）超出未收余额（{Amount - SettledAmount}）。");

        SettledAmount += amount;
        Status = SettledAmount >= Amount
            ? ReceivableStatus.Settled
            : ReceivableStatus.PartiallyPaid;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>退回核销（退款时调用，减少已核销金额）。</summary>
    internal void ReverseSettlement(decimal amount)
    {
        if (amount <= 0 || amount > SettledAmount)
            throw new InvalidOperationException($"退款金额（{amount}）超出已核销金额（{SettledAmount}）。");

        SettledAmount -= amount;
        Status = SettledAmount <= 0
            ? ReceivableStatus.Unpaid
            : ReceivableStatus.PartiallyPaid;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>未收金额。</summary>
    public decimal Outstanding => Amount - SettledAmount;
}
