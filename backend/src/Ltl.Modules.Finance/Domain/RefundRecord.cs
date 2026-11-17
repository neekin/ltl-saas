namespace Ltl.Modules.Finance.Domain;

public sealed class RefundRecord
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PaymentId { get; private set; }
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = null!;
    public Guid? OperatorUserId { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private RefundRecord() { }

    public static RefundRecord Create(
        Guid tenantId, Guid paymentId, decimal amount, string reason,
        Guid? operatorUserId, DateTime occurredAtUtc)
    {
        if (amount <= 0) throw new ArgumentException("退款金额必须 >0。", nameof(amount));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("退款原因不能为空。", nameof(reason));
        if (occurredAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("时间必须为 UTC。", nameof(occurredAtUtc));
        if (tenantId == Guid.Empty) throw new ArgumentException("租户 Id 不能为空 Guid。", nameof(tenantId));
        return new RefundRecord
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PaymentId = paymentId,
            Amount = amount, Reason = reason.Trim(), OperatorUserId = operatorUserId,
            OccurredAt = occurredAtUtc, CreatedAt = occurredAtUtc,
        };
    }
}
