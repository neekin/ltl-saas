namespace Ltl.Modules.Arrivals.Domain;

/// <summary>
/// 到货登记（arrivals，追加式 ARRIVAL-001）：目的网点对运输批次的到货核对。
/// 每批次一次到货登记（ux tenant_id + trip_id）；逐运单差异在 arrival_items 留痕不可覆盖。
/// </summary>
public sealed class CargoArrival : TenantScopedEntity
{
    private readonly List<ArrivalItem> _items = [];

    public Guid TripId { get; private set; }

    /// <summary>到达网点（= 批次终点，服务端校验）。</summary>
    public Guid BranchId { get; private set; }

    public Guid? OperatorUserId { get; private set; }

    public string? Note { get; private set; }

    public DateTime ArrivedAt { get; private set; }

    /// <summary>到货清单（追加式不可覆盖）。</summary>
    public IReadOnlyList<ArrivalItem> Items => _items.AsReadOnly();

    private CargoArrival()
    {
    }

    public static CargoArrival Create(
        Guid tenantId,
        Guid tripId,
        Guid branchId,
        IReadOnlyList<(Guid WaybillId, int LoadedPieces, int ReceivedPieces, int BrokenPieces, string? Remark)> items,
        Guid? operatorUserId,
        string? note,
        DateTime arrivedAtUtc,
        Guid? createdBy = null)
    {
        if (arrivedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("时间必须为 UTC。", nameof(arrivedAtUtc));
        if (items.Count == 0)
            throw new ArgumentException("到货清单至少 1 项。", nameof(items));

        var arrival = new CargoArrival
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            TripId = tripId,
            BranchId = branchId,
            OperatorUserId = operatorUserId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            ArrivedAt = arrivedAtUtc,
            CreatedAt = arrivedAtUtc,
            CreatedBy = createdBy,
        };

        arrival._items.AddRange(items.Select(item => ArrivalItem.Create(
            arrival.TenantId, arrival.Id, item.WaybillId,
            item.LoadedPieces, item.ReceivedPieces, item.BrokenPieces,
            item.Remark, arrivedAtUtc)));
        return arrival;
    }
}
