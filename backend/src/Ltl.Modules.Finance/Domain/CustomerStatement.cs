
namespace Ltl.Modules.Finance.Domain;

/// <summary>月结账单状态。</summary>
public enum CustomerStatementStatus
{
    /// <summary>草稿（可调整明细）。</summary>
    Draft = 1,

    /// <summary>已确认（明细冻结；不可修改）。</summary>
    Confirmed = 2,

    /// <summary>已关闭（终态）。</summary>
    Closed = 3,
}

/// <summary>
/// 月结账单（customer_statements）：按客户按期生成（汇总未收应收）。
/// 确认后明细冻结不可修改（AGENTS §6.5）；关闭为终态。
/// </summary>
public sealed class CustomerStatement : TenantScopedEntity
{
    public Guid CustomerId { get; private set; }

    /// <summary>账期（yyyyMM）。</summary>
    public string Period { get; private set; } = null!;

    public CustomerStatementStatus Status { get; private set; }

    /// <summary>账单总金额（Σ应收）。</summary>
    public decimal TotalAmount { get; private set; }

    public DateTime? ConfirmedAt { get; private set; }

    public Guid? ConfirmedBy { get; private set; }

    public DateTime? ClosedAt { get; private set; }

    public Guid? ClosedBy { get; private set; }

    private CustomerStatement()
    {
    }

    public static CustomerStatement Create(
        Guid tenantId, Guid customerId, string period, decimal totalAmount,
        DateTime nowUtc, Guid? createdBy = null)
    {
        if (nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("时间必须为 UTC。", nameof(nowUtc));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("租户 Id 不能为空 Guid。", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(period) || period.Length != 6)
            throw new ArgumentException("账期格式必须为 yyyyMM。", nameof(period));
        if (totalAmount <= 0)
            throw new ArgumentException("账单总金额必须 >0。", nameof(totalAmount));

        return new CustomerStatement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerId = customerId,
            Period = period,
            TotalAmount = totalAmount,
            Status = CustomerStatementStatus.Draft,
            CreatedAt = nowUtc,
            CreatedBy = createdBy,
        };
    }

    /// <summary>确认（Draft → Confirmed；明细冻结不可修改）。</summary>
    public void Confirm(DateTime nowUtc, Guid? confirmedBy)
    {
        if (Status != CustomerStatementStatus.Draft)
            throw new InvalidOperationException($"账单状态为 {Status}，仅草稿可确认。");
        if (nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("时间必须为 UTC。", nameof(nowUtc));

        Status = CustomerStatementStatus.Confirmed;
        ConfirmedAt = nowUtc;
        ConfirmedBy = confirmedBy;
    }

    /// <summary>关闭（Confirmed → Closed；终态）。</summary>
    public void Close(DateTime nowUtc, Guid? closedBy)
    {
        if (Status != CustomerStatementStatus.Confirmed)
            throw new InvalidOperationException($"账单状态为 {Status}，仅已确认账单可关闭。");
        if (nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("时间必须为 UTC。", nameof(nowUtc));

        Status = CustomerStatementStatus.Closed;
        ClosedAt = nowUtc;
        ClosedBy = closedBy;
    }
}
