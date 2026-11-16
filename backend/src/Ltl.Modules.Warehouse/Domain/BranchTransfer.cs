namespace Ltl.Modules.Warehouse.Domain;

/// <summary>调拨单状态（branch_transfers.status）。</summary>
public enum BranchTransferStatus
{
    /// <summary>草稿（可编辑清单/取消）。</summary>
    Draft = 1,

    /// <summary>已提交（待发出确认）。</summary>
    Submitted = 2,

    /// <summary>已发出（调出网点确认发出；待调入验收）。</summary>
    Dispatched = 3,

    /// <summary>已验收（终态；差异留痕）。</summary>
    Received = 4,

    /// <summary>已取消（草稿/已提交可取消）。</summary>
    Cancelled = 5,
}

/// <summary>
/// 网点调拨单聚合根（branch_transfers，TRANSFER-001）。
/// AGENTS §6.3：同一租户内直营网点交接；记录调出清单、发出确认、调入验收和差异；
/// 调拨不产生对外销售收入。
/// </summary>
public sealed class BranchTransfer : TenantScopedEntity
{
    private readonly List<BranchTransferItem> _items = [];

    public string TransferNo { get; private set; } = null!;

    public Guid FromBranchId { get; private set; }

    public Guid ToBranchId { get; private set; }

    public BranchTransferStatus Status { get; private set; }

    public string? Note { get; private set; }

    public DateTime? SubmittedAt { get; private set; }

    public Guid? SubmittedBy { get; private set; }

    public DateTime? DispatchedAt { get; private set; }

    public Guid? DispatchedBy { get; private set; }

    public DateTime? ReceivedAt { get; private set; }

    public Guid? ReceivedBy { get; private set; }

    /// <summary>调出清单（验收后不可变）。</summary>
    public IReadOnlyList<BranchTransferItem> Items => _items.AsReadOnly();

    private BranchTransfer()
    {
    }

    /// <summary>创建调拨单（草稿）。起点≠终点；清单至少 1 项，序号 1..N。</summary>
    public static BranchTransfer Create(
        Guid tenantId, string transferNo, Guid fromBranchId, Guid toBranchId,
        IReadOnlyList<(string CargoName, int Quantity, string? Remark)> items,
        DateTime nowUtc, Guid? createdBy = null, string? note = null)
    {
        if (fromBranchId == Guid.Empty || toBranchId == Guid.Empty)
        {
            throw new ArgumentException("调出/调入网点 Id 不能为空 Guid。");
        }

        if (fromBranchId == toBranchId)
        {
            throw new ArgumentException("调出网点与调入网点不能相同。");
        }

        if (items.Count == 0)
        {
            throw new ArgumentException("调拨清单至少 1 项。", nameof(items));
        }

        var created = EnsureUtc(nowUtc);
        var transfer = new BranchTransfer
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            TransferNo = RequireNonBlank(transferNo, "调拨单号", 64),
            FromBranchId = fromBranchId,
            ToBranchId = toBranchId,
            Status = BranchTransferStatus.Draft,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAt = created,
            CreatedBy = createdBy,
        };

        transfer._items.AddRange(items.Select((item, index) => BranchTransferItem.Create(
            transfer.TenantId, transfer.Id, index + 1, item.CargoName, item.Quantity, created)));
        return transfer;
    }

    /// <summary>提交（Draft → Submitted；提交后清单不可改，可取消）。</summary>
    public void Submit(DateTime nowUtc, Guid? operatorUserId)
    {
        if (Status != BranchTransferStatus.Draft)
        {
            throw new WarehouseDomainException($"调拨单 {TransferNo} 状态为 {Status}，仅草稿可提交。");
        }

        Status = BranchTransferStatus.Submitted;
        SubmittedAt = EnsureUtc(nowUtc);
        SubmittedBy = operatorUserId;
        Touch(nowUtc, operatorUserId);
    }

    /// <summary>发出确认（Submitted → Dispatched；调出网点动作）。</summary>
    public void Dispatch(DateTime nowUtc, Guid? operatorUserId)
    {
        if (Status != BranchTransferStatus.Submitted)
        {
            throw new WarehouseDomainException($"调拨单 {TransferNo} 状态为 {Status}，仅已提交调拨单可发出。");
        }

        Status = BranchTransferStatus.Dispatched;
        DispatchedAt = EnsureUtc(nowUtc);
        DispatchedBy = operatorUserId;
        Touch(nowUtc, operatorUserId);
    }

    /// <summary>取消（Draft/Submitted → Cancelled；终态）。</summary>
    public void Cancel(DateTime nowUtc, Guid? operatorUserId)
    {
        if (Status is not (BranchTransferStatus.Draft or BranchTransferStatus.Submitted))
        {
            throw new WarehouseDomainException($"调拨单 {TransferNo} 状态为 {Status}，不可取消。");
        }

        Status = BranchTransferStatus.Cancelled;
        Touch(nowUtc, operatorUserId);
    }

    /// <summary>
    /// 调入验收（Dispatched → Received；一次性写入实收/破损，原始交接记录不可覆盖）。
    /// 破损计入实收：received ≥ broken；received ≤ declared（超收差异随 P6 异常件细化）。
    /// </summary>
    public void Receive(
        IReadOnlyList<(Guid ItemId, int ReceivedQuantity, int BrokenQuantity, string? Remark)> receipts,
        DateTime nowUtc, Guid? operatorUserId)
    {
        if (Status != BranchTransferStatus.Dispatched)
        {
            throw new WarehouseDomainException($"调拨单 {TransferNo} 状态为 {Status}，仅已发出调拨单可验收。");
        }

        var byId = receipts.ToDictionary(r => r.ItemId);
        foreach (var item in _items)
        {
            if (!byId.TryGetValue(item.Id, out var receipt))
            {
                throw new WarehouseDomainException($"调拨清单第 {item.Seq} 项缺少验收数量。");
            }

            item.Receive(receipt.ReceivedQuantity, receipt.BrokenQuantity, receipt.Remark);
        }

        Status = BranchTransferStatus.Received;
        ReceivedAt = EnsureUtc(nowUtc);
        ReceivedBy = operatorUserId;
        Touch(nowUtc, operatorUserId);
    }
}
