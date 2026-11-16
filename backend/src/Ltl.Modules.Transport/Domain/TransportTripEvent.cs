using System.Text.Json.Serialization;

namespace Ltl.Modules.Transport.Domain;

/// <summary>运输事件类型白名单（transport_trip_events.event_type）。</summary>
public static class TransportEventTypes
{
    public const string Departed = "departed";
    public const string Arrived = "arrived";
}

/// <summary>运输批次事件（transport_trip_events，追加式 TRIP-002）：批次维度发车/到达留痕。</summary>
public sealed class TransportTripEvent
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid TripId { get; private set; }

    public string EventType { get; private set; } = null!;

    /// <summary>事件摘要（JSONB：如配载运单数、到达网点）。</summary>
    public string? Detail { get; private set; }

    public Guid? OperatorUserId { get; private set; }

    public DateTime OccurredAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private TransportTripEvent()
    {
    }

    public static TransportTripEvent Create(
        Guid tenantId, Guid tripId, string eventType,
        object? detail, Guid? operatorUserId, DateTime occurredAtUtc)
    {
        if (eventType is not (TransportEventTypes.Departed or TransportEventTypes.Arrived))
        {
            throw new ArgumentException($"未知运输事件类型（{eventType}）。", nameof(eventType));
        }

        if (occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("时间必须为 UTC。", nameof(occurredAtUtc));
        }

        return new TransportTripEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId == Guid.Empty
                ? throw new ArgumentException("租户 Id 不能为空 Guid。")
                : tenantId,
            TripId = tripId,
            EventType = eventType,
            Detail = detail is null
                ? null
                : System.Text.Json.JsonSerializer.Serialize(detail,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    }),
            OperatorUserId = operatorUserId,
            OccurredAt = occurredAtUtc,
            CreatedAt = occurredAtUtc,
        };
    }
}
