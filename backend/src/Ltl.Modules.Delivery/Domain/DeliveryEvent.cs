namespace Ltl.Modules.Delivery.Domain;

/// <summary>交付事件（delivery_events，追加式）：created / completed / rejected。</summary>
public sealed class DeliveryEvent
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid DeliveryOrderId { get; private set; }
    public string EventType { get; private set; } = null!;
    public string? Detail { get; private set; }
    public Guid? OperatorUserId { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private DeliveryEvent() { }

    public static DeliveryEvent Create(
        Guid tenantId, Guid deliveryOrderId, string eventType,
        string? detailJson, Guid? operatorUserId, DateTime occurredAtUtc)
    {
        if (occurredAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("时间必须为 UTC。", nameof(occurredAtUtc));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("租户 Id 不能为空 Guid。", nameof(tenantId));

        return new DeliveryEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DeliveryOrderId = deliveryOrderId,
            EventType = eventType,
            Detail = detailJson,
            OperatorUserId = operatorUserId,
            OccurredAt = occurredAtUtc,
            CreatedAt = occurredAtUtc,
        };
    }
}
