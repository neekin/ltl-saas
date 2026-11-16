namespace Ltl.Modules.Transport.Domain;

/// <summary>运输费用类型白名单（transport_trip_fees.fee_type）。追加类型 = 扩展清单 + 校验。</summary>
public static class TransportFeeTypes
{
    public const string Fuel = "fuel";
    public const string Toll = "toll";
    public const string Driver = "driver";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = [Fuel, Toll, Driver, Other];
}

/// <summary>运输费用记录（transport_trip_fees，追加式 TRIP-002）：错误经冲正处理，不修改删除。</summary>
public sealed class TransportTripFee
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid TripId { get; private set; }

    public string FeeType { get; private set; } = null!;

    public decimal Amount { get; private set; }

    public string? Note { get; private set; }

    public Guid? OperatorUserId { get; private set; }

    public DateTime OccurredAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private TransportTripFee()
    {
    }

    public static TransportTripFee Create(
        Guid tenantId, Guid tripId, string feeType, decimal amount,
        Guid? operatorUserId, string? note, DateTime occurredAtUtc)
    {
        if (!TransportFeeTypes.All.Contains(feeType))
        {
            throw new ArgumentException($"未知运输费用类型（{feeType}）。", nameof(feeType));
        }

        if (amount < 0)
        {
            throw new ArgumentException("运输费用金额不能为负数。", nameof(amount));
        }

        if (occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("时间必须为 UTC。", nameof(occurredAtUtc));
        }

        return new TransportTripFee
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId == Guid.Empty
                ? throw new ArgumentException("租户 Id 不能为空 Guid。")
                : tenantId,
            TripId = tripId,
            FeeType = feeType,
            Amount = amount,
            OperatorUserId = operatorUserId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            OccurredAt = occurredAtUtc,
            CreatedAt = occurredAtUtc,
        };
    }
}
