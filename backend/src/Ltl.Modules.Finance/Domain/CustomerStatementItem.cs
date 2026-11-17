namespace Ltl.Modules.Finance.Domain;

/// <summary>
/// 月结账单明细（customer_statement_items）：关联逐张应收，保留生成时点的快照金额。
/// 确认后不可修改（AGENTS §6.5）。
/// </summary>
public sealed class CustomerStatementItem
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid StatementId { get; private set; }

    public Guid ReceivableId { get; private set; }

    public Guid WaybillId { get; private set; }

    /// <summary>生成时点的应收快照金额。</summary>
    public decimal SnapshotAmount { get; private set; }

    /// <summary>生成时点的已核销金额。</summary>
    public decimal SnapshotSettledAmount { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private CustomerStatementItem()
    {
    }

    public static CustomerStatementItem Create(
        Guid tenantId, Guid statementId, Guid receivableId, Guid waybillId,
        decimal snapshotAmount, decimal snapshotSettledAmount, DateTime createdAtUtc)
    {
        if (createdAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("时间必须为 UTC。", nameof(createdAtUtc));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("租户 Id 不能为空 Guid。", nameof(tenantId));

        return new CustomerStatementItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StatementId = statementId,
            ReceivableId = receivableId,
            WaybillId = waybillId,
            SnapshotAmount = snapshotAmount,
            SnapshotSettledAmount = snapshotSettledAmount,
            CreatedAt = createdAtUtc,
        };
    }
}
