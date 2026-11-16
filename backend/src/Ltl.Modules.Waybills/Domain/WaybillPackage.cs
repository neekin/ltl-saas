namespace Ltl.Modules.Waybills.Domain;

/// <summary>
/// 开单时的货物件草稿（随 Waybill 工厂创建，件号由聚合统一编号 1..N）。
/// </summary>
public sealed record WaybillPackageDraft(
    string CargoName,
    int Quantity,
    decimal WeightKg,
    decimal VolumeM3,
    string? Remark = null);

/// <summary>
/// 运单货物件明细（一票多件）。仅 Draft 运单可调整（守卫在 Waybill 聚合上）；
/// 重量 NUMERIC(12,3)、体积 NUMERIC(12,4)（AGENTS §7）。
/// </summary>
public sealed class WaybillPackage : TenantScopedEntity
{
    public Guid WaybillId { get; private set; }

    /// <summary>件号（1..N 连续，同运单唯一）。</summary>
    public int PackageNo { get; private set; }

    /// <summary>货名。</summary>
    public string CargoName { get; private set; } = null!;

    /// <summary>本明细件数（≥1）。</summary>
    public int Quantity { get; private set; }

    /// <summary>重量（kg，≥0，精度 3 位）。</summary>
    public decimal WeightKg { get; private set; }

    /// <summary>体积（m³，≥0，精度 4 位）。</summary>
    public decimal VolumeM3 { get; private set; }

    public string? Remark { get; private set; }

    private WaybillPackage()
    {
    }

    /// <summary>由聚合工厂调用；独立调用场景仅限持久层测试（聚合内守卫仍由 Waybill 执行）。</summary>
    public static WaybillPackage Create(
        Guid tenantId,
        Guid waybillId,
        int packageNo,
        WaybillPackageDraft draft,
        DateTime nowUtc,
        Guid? createdBy)
    {
        return new WaybillPackage
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            WaybillId = waybillId,
            PackageNo = packageNo,
            CargoName = RequireNonBlank(draft.CargoName, "货名", 100),
            Quantity = draft.Quantity >= 1
                ? draft.Quantity
                : throw new ArgumentException("货物件件数必须 ≥1。", nameof(draft)),
            WeightKg = draft.WeightKg >= 0
                ? draft.WeightKg
                : throw new ArgumentException("货物件重量不能为负数。", nameof(draft)),
            VolumeM3 = draft.VolumeM3 >= 0
                ? draft.VolumeM3
                : throw new ArgumentException("货物件体积不能为负数。", nameof(draft)),
            Remark = string.IsNullOrWhiteSpace(draft.Remark) ? null : draft.Remark.Trim(),
            CreatedAt = EnsureUtc(nowUtc),
            CreatedBy = createdBy,
        };
    }

    /// <summary>更新货物件内容（仅 Draft；守卫由聚合执行）。件号不变。</summary>
    internal void UpdateDraft(
        WaybillPackageDraft draft, DateTime nowUtc, Guid? updatedBy)
    {
        CargoName = RequireNonBlank(draft.CargoName, "货名", 100);
        Quantity = draft.Quantity >= 1
            ? draft.Quantity
            : throw new ArgumentException("货物件件数必须 ≥1。", nameof(draft));
        WeightKg = draft.WeightKg >= 0
            ? draft.WeightKg
            : throw new ArgumentException("货物件重量不能为负数。", nameof(draft));
        VolumeM3 = draft.VolumeM3 >= 0
            ? draft.VolumeM3
            : throw new ArgumentException("货物件体积不能为负数。", nameof(draft));
        Remark = string.IsNullOrWhiteSpace(draft.Remark) ? null : draft.Remark.Trim();
        Touch(nowUtc, updatedBy);
    }

    /// <summary>移除货物件后由聚合重排件号（1..N 连续）。</summary>
    internal void Renumber(int packageNo) => PackageNo = packageNo;
}
