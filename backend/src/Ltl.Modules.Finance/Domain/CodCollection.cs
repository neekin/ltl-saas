namespace Ltl.Modules.Finance.Domain;

/// <summary>COD 收款记录（cod_collections，追加式 AGENTS §6.5）。</summary>
public sealed class CodCollection
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CodOrderId { get; private set; }
    public decimal Amount { get; private set; }
    public Guid? OperatorUserId { get; private set; }
    public string? Note { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private CodCollection() { }

    public static CodCollection Create(
        Guid tenantId, Guid codOrderId, decimal amount,
        Guid? operatorUserId, string? note, DateTime occurredAtUtc)
    {
        if (amount <= 0) throw new ArgumentException("COD 收款金额必须 >0。", nameof(amount));
        if (occurredAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("时间必须为 UTC。", nameof(occurredAtUtc));
        if (tenantId == Guid.Empty) throw new ArgumentException("租户 Id 不能为空 Guid。", nameof(tenantId));
        return new CodCollection
        {
            Id = Guid.NewGuid(), TenantId = tenantId, CodOrderId = codOrderId,
            Amount = amount, OperatorUserId = operatorUserId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            OccurredAt = occurredAtUtc, CreatedAt = occurredAtUtc,
        };
    }
}
