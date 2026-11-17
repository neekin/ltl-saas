namespace Ltl.Modules.Finance.Domain;

/// <summary>
/// COD 退款/冲正记录（cod_refund_records，追加式 AGENTS §6.5）：
/// 关联原 COD 收款（保留原交易 + 反向交易）；退款后 COD 订单 collected_amount 回退。
/// </summary>
public sealed class CodRefundRecord
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>原 COD 收款 ID（退款关联原交易）。</summary>
    public Guid CollectionId { get; private set; }

    public decimal Amount { get; private set; }

    public string Reason { get; private set; } = null!;

    public Guid? OperatorUserId { get; private set; }

    public DateTime OccurredAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private CodRefundRecord()
    {
    }

    public static CodRefundRecord Create(
        Guid tenantId, Guid collectionId, decimal amount, string reason,
        Guid? operatorUserId, DateTime occurredAtUtc)
    {
        if (amount <= 0)
            throw new ArgumentException("COD 退款金额必须 >0。", nameof(amount));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("退款原因不能为空。", nameof(reason));
        if (occurredAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("时间必须为 UTC。", nameof(occurredAtUtc));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("租户 Id 不能为空 Guid。", nameof(tenantId));

        return new CodRefundRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CollectionId = collectionId,
            Amount = amount,
            Reason = reason.Trim(),
            OperatorUserId = operatorUserId,
            OccurredAt = occurredAtUtc,
            CreatedAt = occurredAtUtc,
        };
    }
}
