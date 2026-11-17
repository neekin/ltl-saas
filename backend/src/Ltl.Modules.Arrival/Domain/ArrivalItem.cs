namespace Ltl.Modules.Arrivals.Domain;

/// <summary>
/// 到货清单行（arrival_items，追加式）：逐运单实收件数/破损 vs 装车快照，差异留痕不可覆盖。
/// 每运单仅一次到货（ux tenant_id + waybill_id）。
/// </summary>
public sealed class ArrivalItem
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ArrivalId { get; private set; }

    public Guid WaybillId { get; private set; }

    /// <summary>装车件数（发车时快照）。</summary>
    public int LoadedPieces { get; private set; }

    /// <summary>实收件数（含破损）。</summary>
    public int ReceivedPieces { get; private set; }

    /// <summary>破损件数（计入实收）。</summary>
    public int BrokenPieces { get; private set; }

    public string? Remark { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private ArrivalItem()
    {
    }

    public static ArrivalItem Create(
        Guid tenantId, Guid arrivalId, Guid waybillId,
        int loadedPieces, int receivedPieces, int brokenPieces,
        string? remark, DateTime createdAtUtc)
    {
        if (loadedPieces < 1)
            throw new ArgumentException("装车件数必须 ≥1。", nameof(loadedPieces));
        if (receivedPieces < 0 || brokenPieces < 0)
            throw new ArgumentException("实收/破损件数不能为负数。");
        if (brokenPieces > receivedPieces)
            throw new ArgumentException($"破损件数（{brokenPieces}）不能大于实收件数（{receivedPieces}）。");
        if (receivedPieces > loadedPieces)
            throw new ArgumentException($"实收件数（{receivedPieces}）超出装车件数（{loadedPieces}）。");
        if (createdAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("时间必须为 UTC。", nameof(createdAtUtc));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("租户 Id 不能为空 Guid。", nameof(tenantId));

        return new ArrivalItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ArrivalId = arrivalId,
            WaybillId = waybillId,
            LoadedPieces = loadedPieces,
            ReceivedPieces = receivedPieces,
            BrokenPieces = brokenPieces,
            Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim(),
            CreatedAt = createdAtUtc,
        };
    }
}
