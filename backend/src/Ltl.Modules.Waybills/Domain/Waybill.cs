namespace Ltl.Modules.Waybills.Domain;

/// <summary>
/// 运单聚合根：一次托运业务的单据，运单号租户内唯一。
/// 六类状态独立维护（AGENTS §6.1）：运单/货物/运输/交付/运费财务/COD——本任务实现运单状态机
/// （确认/取消）与 Draft 调整边界；其余状态初始值落库，流转随对应业务任务交付。
/// </summary>
public sealed class Waybill : TenantScopedEntity
{
    private readonly List<WaybillPackage> _packages = [];

    /// <summary>运单号（SEQ-001 生成：YB+网点码+期间+序号），租户内唯一。</summary>
    public string WaybillNo { get; private set; } = null!;

    public Guid OriginBranchId { get; private set; }

    public Guid DestBranchId { get; private set; }

    /// <summary>发货客户。</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>业务线路（开单可不指定，配载前可空）。</summary>
    public Guid? RouteId { get; private set; }

    public WaybillPaymentType PaymentType { get; private set; }

    // ---- 六类分离状态 ----

    public WaybillStatus Status { get; private set; }

    public WaybillCargoStatus CargoStatus { get; private set; }

    public WaybillTransportStatus TransportStatus { get; private set; }

    public WaybillDeliveryStatus DeliveryStatus { get; private set; }

    public WaybillFreightStatus FreightStatus { get; private set; }

    public WaybillCodStatus CodStatus { get; private set; }

    // ---- 资金字段（COD 独立于运费，AGENTS §6.5）----

    /// <summary>代收货款金额（null/0 = 无代收；不计入运费总额）。</summary>
    public decimal? CodAmount { get; private set; }

    /// <summary>运单总费用（计价固化时写入，WB-002）；未计价为 null。</summary>
    public decimal? TotalAmount { get; private set; }

    // ---- 收发货人快照（开单时文本固化，不受客户资料后续变更影响）----

    public string SenderName { get; private set; } = null!;

    public string SenderPhone { get; private set; } = null!;

    public string SenderAddress { get; private set; } = null!;

    public string ReceiverName { get; private set; } = null!;

    public string ReceiverPhone { get; private set; } = null!;

    public string ReceiverAddress { get; private set; } = null!;

    public string? Remark { get; private set; }

    /// <summary>开单幂等键（WB-002：重复提交返回同一运单；null = 未携带）。</summary>
    public Guid? IdempotencyKey { get; private set; }

    /// <summary>货物件明细（件号 1..N 连续）。</summary>
    public IReadOnlyList<WaybillPackage> Packages => _packages.AsReadOnly();

    private Waybill()
    {
    }

    /// <summary>开单创建（草稿状态）。货物件至少 1 件，件号统一编为 1..N。</summary>
    /// <exception cref="ArgumentException">参数非法时抛出。</exception>
    public static Waybill Create(
        Guid tenantId,
        string waybillNo,
        Guid originBranchId,
        Guid destBranchId,
        Guid customerId,
        WaybillPaymentType paymentType,
        string senderName,
        string senderPhone,
        string senderAddress,
        string receiverName,
        string receiverPhone,
        string receiverAddress,
        IReadOnlyList<WaybillPackageDraft> packages,
        DateTime nowUtc,
        decimal? codAmount = null,
        Guid? routeId = null,
        string? remark = null,
        Guid? createdBy = null,
        Guid? idempotencyKey = null)
    {
        var now = EnsureUtc(nowUtc);

        if (originBranchId == Guid.Empty || destBranchId == Guid.Empty)
        {
            throw new ArgumentException("始发/目的网点 Id 不能为空 Guid。");
        }

        if (originBranchId == destBranchId)
        {
            throw new ArgumentException("始发网点与目的网点不能相同。");
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("客户 Id 不能为空 Guid。");
        }

        if (packages.Count == 0)
        {
            throw new ArgumentException("运单必须至少包含 1 个货物件。", nameof(packages));
        }

        if (codAmount is { } cod && cod < 0)
        {
            throw new ArgumentException("代收货款金额不能为负数。", nameof(codAmount));
        }

        var waybill = new Waybill
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            WaybillNo = RequireNonBlank(waybillNo, "运单号", 64),
            OriginBranchId = originBranchId,
            DestBranchId = destBranchId,
            CustomerId = customerId,
            RouteId = routeId,
            PaymentType = paymentType,
            Status = WaybillStatus.Draft,
            CargoStatus = WaybillCargoStatus.PendingReceive,
            TransportStatus = WaybillTransportStatus.NotShipped,
            DeliveryStatus = WaybillDeliveryStatus.NotDelivered,
            FreightStatus = WaybillFreightStatus.Unpaid,
            CodStatus = codAmount is > 0
                ? WaybillCodStatus.PendingCollection
                : WaybillCodStatus.None,
            CodAmount = codAmount is > 0 ? codAmount : null,
            SenderName = RequireNonBlank(senderName, "发货人", 100),
            SenderPhone = RequireNonBlank(senderPhone, "发货人电话", 50),
            SenderAddress = RequireNonBlank(senderAddress, "发货地址", 500),
            ReceiverName = RequireNonBlank(receiverName, "收货人", 100),
            ReceiverPhone = RequireNonBlank(receiverPhone, "收货人电话", 50),
            ReceiverAddress = RequireNonBlank(receiverAddress, "收货地址", 500),
            Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim(),
            IdempotencyKey = idempotencyKey,
            CreatedAt = now,
            CreatedBy = createdBy,
        };

        waybill._packages.AddRange(packages.Select((draft, index) => WaybillPackage.Create(
            waybill.TenantId, waybill.Id, index + 1, draft, now, createdBy)));
        return waybill;
    }

    /// <summary>确认运单（仅 Draft；确认后货物件与资料不可再调整，进入收货/运输流程）。</summary>
    /// <exception cref="WaybillDomainException">非 Draft 状态时抛出。</exception>
    public void Confirm(DateTime nowUtc, Guid? confirmedBy)
    {
        if (Status != WaybillStatus.Draft)
        {
            throw new WaybillDomainException($"运单 {WaybillNo} 状态为 {Status}，仅草稿可确认。");
        }

        Status = WaybillStatus.Confirmed;
        Touch(nowUtc, confirmedBy);
    }

    /// <summary>取消运单（Draft/Confirmed，且仅限未发运；Cancelled 为终态）。</summary>
    /// <exception cref="WaybillDomainException">已取消或已发运时抛出。</exception>
    public void Cancel(DateTime nowUtc, Guid? cancelledBy)
    {
        if (Status == WaybillStatus.Cancelled)
        {
            throw new WaybillDomainException($"运单 {WaybillNo} 已取消（终态），不可重复取消。");
        }

        if (TransportStatus != WaybillTransportStatus.NotShipped)
        {
            throw new WaybillDomainException($"运单 {WaybillNo} 已发运，不可取消。");
        }

        Status = WaybillStatus.Cancelled;
        Touch(nowUtc, cancelledBy);
    }

    /// <summary>新增货物件（仅 Draft；件号接续编至尾部）。</summary>
    /// <exception cref="WaybillDomainException">非 Draft 状态时抛出。</exception>
    public void AddPackage(WaybillPackageDraft draft, DateTime nowUtc, Guid? updatedBy)
    {
        RequireDraft("新增货物件");
        _packages.Add(WaybillPackage.Create(
            TenantId, Id, _packages.Count + 1, draft, EnsureUtc(nowUtc), updatedBy));
        Touch(nowUtc, updatedBy);
    }

    /// <summary>更新货物件内容（仅 Draft）。</summary>
    /// <exception cref="WaybillDomainException">非 Draft 状态或件不存在时抛出。</exception>
    public void UpdatePackage(
        Guid packageId, WaybillPackageDraft draft, DateTime nowUtc, Guid? updatedBy)
    {
        RequireDraft("更新货物件");
        var package = _packages.SingleOrDefault(p => p.Id == packageId)
            ?? throw new WaybillDomainException($"货物件（{packageId}）不存在。");
        package.UpdateDraft(draft, EnsureUtc(nowUtc), updatedBy);
        Touch(nowUtc, updatedBy);
    }

    /// <summary>移除货物件并重排件号为 1..N 连续（仅 Draft；运单至少保留 1 件）。</summary>
    /// <exception cref="WaybillDomainException">非 Draft、件不存在或只剩 1 件时抛出。</exception>
    public void RemovePackage(Guid packageId, DateTime nowUtc, Guid? updatedBy)
    {
        RequireDraft("移除货物件");
        var package = _packages.SingleOrDefault(p => p.Id == packageId)
            ?? throw new WaybillDomainException($"货物件（{packageId}）不存在。");

        if (_packages.Count == 1)
        {
            throw new WaybillDomainException("运单必须至少保留 1 个货物件。");
        }

        _packages.Remove(package);
        for (var i = 0; i < _packages.Count; i++)
        {
            _packages[i].Renumber(i + 1);
        }

        Touch(nowUtc, updatedBy);
    }

    /// <summary>计价固化总费用（仅 Draft；WB-002 开单计价调用，快照明细见 WB-002）。</summary>
    /// <exception cref="WaybillDomainException">非 Draft、金额非法或已固化时抛出。</exception>
    public void ApplyPricing(decimal totalAmount, DateTime nowUtc, Guid? updatedBy)
    {
        RequireDraft("固化费用");
        if (totalAmount < 0)
        {
            throw new ArgumentException("运单总费用不能为负数。", nameof(totalAmount));
        }

        if (TotalAmount is not null)
        {
            throw new WaybillDomainException($"运单 {WaybillNo} 费用已固化，不可覆盖；重新计价为显式业务操作（WB-002 设计）。");
        }

        TotalAmount = totalAmount;
        Touch(nowUtc, updatedBy);
    }

    /// <summary>汇总件数（Σ货物件件数）。</summary>
    public int TotalPieces => _packages.Sum(p => p.Quantity);

    /// <summary>汇总重量（Σ，kg）。</summary>
    public decimal TotalWeightKg => _packages.Sum(p => p.WeightKg);

    /// <summary>汇总体积（Σ，m³）。</summary>
    public decimal TotalVolumeM3 => _packages.Sum(p => p.VolumeM3);

    /// <summary>
    /// 货物/仓储状态流转：收货登记（RECEIVE-001）。仅已确认运单、待收货状态可收货；
    /// 分批收货后续登记不再改变状态（调用方校验）。
    /// </summary>
    /// <exception cref="WaybillDomainException">未确认或已收货（非待收货）时抛出。</exception>
    public void MarkCargoReceived(DateTime nowUtc, Guid? operatorUserId)
    {
        if (Status != WaybillStatus.Confirmed)
        {
            throw new WaybillDomainException($"运单 {WaybillNo} 状态为 {Status}，仅已确认运单可收货。");
        }

        if (CargoStatus != WaybillCargoStatus.PendingReceive)
        {
            throw new WaybillDomainException($"运单 {WaybillNo} 货物状态为 {CargoStatus}，无需重复收货登记。");
        }

        CargoStatus = WaybillCargoStatus.Received;
        Touch(nowUtc, operatorUserId);
    }

    /// <summary>
    /// 货物/仓储状态流转：分拣完成（WAREHOUSE-001）。仅已收货可分拣。
    /// </summary>
    /// <exception cref="WaybillDomainException">未收货或已分拣时抛出。</exception>
    public void MarkCargoSorted(DateTime nowUtc, Guid? operatorUserId)
    {
        if (CargoStatus != WaybillCargoStatus.Received)
        {
            throw new WaybillDomainException($"运单 {WaybillNo} 货物状态为 {CargoStatus}，仅已收货运单可分拣。");
        }

        CargoStatus = WaybillCargoStatus.Sorted;
        Touch(nowUtc, operatorUserId);
    }

    private void RequireDraft(string operation)
    {
        if (Status != WaybillStatus.Draft)
        {
            throw new WaybillDomainException($"运单 {WaybillNo} 状态为 {Status}，仅草稿可{operation}。");
        }
    }
}
