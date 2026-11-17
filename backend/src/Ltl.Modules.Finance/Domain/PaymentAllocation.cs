namespace Ltl.Modules.Finance.Domain;

public sealed class PaymentAllocation
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PaymentId { get; private set; }
    public Guid ReceivableId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PaymentAllocation() { }

    public static PaymentAllocation Create(
        Guid tenantId, Guid paymentId, Guid receivableId, decimal amount, DateTime createdAtUtc)
    {
        if (amount <= 0) throw new ArgumentException("核销金额必须 >0。", nameof(amount));
        if (createdAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("时间必须为 UTC。", nameof(createdAtUtc));
        if (tenantId == Guid.Empty) throw new ArgumentException("租户 Id 不能为空 Guid。", nameof(tenantId));
        return new PaymentAllocation
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PaymentId = paymentId,
            ReceivableId = receivableId, Amount = amount, CreatedAt = createdAtUtc,
        };
    }
}
