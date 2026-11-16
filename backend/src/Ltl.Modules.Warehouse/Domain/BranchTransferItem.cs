namespace Ltl.Modules.Warehouse.Domain;

/// <summary>
/// 调拨清单行（branch_transfer_items）。验收一次性写入实收/破损（守卫），不可覆盖（AGENTS §6.3）。
/// </summary>
public sealed class BranchTransferItem
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid TransferId { get; private set; }

    public int Seq { get; private set; }

    public string CargoName { get; private set; } = null!;

    /// <summary>调出申报数量。</summary>
    public int DeclaredQuantity { get; private set; }

    /// <summary>实收数量（验收写入；null = 未验收）。</summary>
    public int? ReceivedQuantity { get; private set; }

    /// <summary>破损数量（计入实收）。</summary>
    public int? BrokenQuantity { get; private set; }

    public string? Remark { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private BranchTransferItem()
    {
    }

    internal static BranchTransferItem Create(
        Guid tenantId, Guid transferId, int seq, string cargoName, int quantity, DateTime createdAtUtc)
    {
        if (seq < 1)
        {
            throw new ArgumentException("清单序号必须 ≥1。", nameof(seq));
        }

        if (quantity < 1)
        {
            throw new ArgumentException("调拨数量必须 ≥1。", nameof(quantity));
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("时间必须为 UTC。", nameof(createdAtUtc));
        }

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("租户 Id 不能为空 Guid。", nameof(tenantId));
        }

        return new BranchTransferItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TransferId = transferId,
            Seq = seq,
            CargoName = cargoName.Trim(),
            DeclaredQuantity = quantity,
            CreatedAt = createdAtUtc,
        };
    }

    /// <summary>验收写入（仅一次；破损计入实收；received ≤ declared）。</summary>
    /// <exception cref="WarehouseDomainException">重复验收或数量非法时抛出。</exception>
    internal void Receive(int receivedQuantity, int brokenQuantity, string? remark)
    {
        if (ReceivedQuantity is not null)
        {
            throw new WarehouseDomainException($"清单第 {Seq} 项已验收，原始交接记录不可覆盖（AGENTS §6.3）。");
        }

        if (receivedQuantity < 0 || brokenQuantity < 0)
        {
            throw new ArgumentException("实收/破损数量不能为负数。");
        }

        if (brokenQuantity > receivedQuantity)
        {
            throw new WarehouseDomainException($"清单第 {Seq} 项破损数量（{brokenQuantity}）不能大于实收数量（{receivedQuantity}）。");
        }

        if (receivedQuantity > DeclaredQuantity)
        {
            throw new WarehouseDomainException(
                $"清单第 {Seq} 项实收（{receivedQuantity}）超出申报（{DeclaredQuantity}），超收差异请走异常件流程（P6）。");
        }

        ReceivedQuantity = receivedQuantity;
        BrokenQuantity = brokenQuantity;
        Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim();
    }
}
