namespace Ltl.Modules.Finance.Domain;

public enum PaymentMethod
{
    Cash = 1,
    OnDelivery = 2,
    Monthly = 3,
}

public sealed class PaymentRecord : TenantScopedEntity
{
    public Guid WaybillId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentMethod Method { get; private set; }
    public Guid? OperatorUserId { get; private set; }
    public string? Note { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private PaymentRecord() { }

    public static PaymentRecord Create(
        Guid tenantId, Guid waybillId, decimal amount, PaymentMethod method,
        Guid? operatorUserId, string? note, DateTime occurredAtUtc, Guid? createdBy = null)
    {
        if (amount <= 0) throw new ArgumentException("收款金额必须 >0。", nameof(amount));
        if (occurredAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("时间必须为 UTC。", nameof(occurredAtUtc));
        if (tenantId == Guid.Empty) throw new ArgumentException("租户 Id 不能为空 Guid。", nameof(tenantId));
        return new PaymentRecord
        {
            Id = Guid.NewGuid(), TenantId = tenantId, WaybillId = waybillId,
            Amount = amount, Method = method, OperatorUserId = operatorUserId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            OccurredAt = occurredAtUtc, CreatedAt = occurredAtUtc, CreatedBy = createdBy,
        };
    }
}
