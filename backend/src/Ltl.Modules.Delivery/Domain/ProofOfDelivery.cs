namespace Ltl.Modules.Delivery.Domain;

/// <summary>交付凭证（proof_of_deliveries，每交付单一张）。签收后创建；照片为可选文件引用。</summary>
public sealed class ProofOfDelivery
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid DeliveryOrderId { get; private set; }
    public string ReceiverName { get; private set; } = null!;
    public Guid? PhotoFileId { get; private set; }
    public DateTime SignedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ProofOfDelivery() { }

    public static ProofOfDelivery Create(
        Guid tenantId, Guid deliveryOrderId, string receiverName,
        Guid? photoFileId, DateTime signedAtUtc)
    {
        if (signedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("时间必须为 UTC。", nameof(signedAtUtc));
        if (string.IsNullOrWhiteSpace(receiverName))
            throw new ArgumentException("签收人不能为空。", nameof(receiverName));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("租户 Id 不能为空 Guid。", nameof(tenantId));

        return new ProofOfDelivery
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DeliveryOrderId = deliveryOrderId,
            ReceiverName = receiverName.Trim(),
            PhotoFileId = photoFileId,
            SignedAt = signedAtUtc,
            CreatedAt = signedAtUtc,
        };
    }
}
