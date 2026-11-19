using Ltl.Modules.Pricing.Application;
using Ltl.Modules.Pricing.Domain;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Platform.Application;
using Ltl.Modules.Waybills.Domain;
using Ltl.Modules.Waybills.Infrastructure;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Ltl.Modules.Waybills.Application;

public sealed record WaybillListItemDto(
    Guid Id, string WaybillNo, string Status, string CargoStatus, string TransportStatus,
    string DeliveryStatus, string FreightStatus, string CodStatus,
    Guid OriginBranchId, Guid DestBranchId, decimal? TotalAmount, DateTime CreatedAt);

public sealed record WaybillDetailDto(
    Guid Id, string WaybillNo, WaybillStatus Status, WaybillCargoStatus CargoStatus,
    WaybillTransportStatus TransportStatus, WaybillDeliveryStatus DeliveryStatus,
    WaybillFreightStatus FreightStatus, WaybillCodStatus CodStatus,
    Guid OriginBranchId, Guid DestBranchId, Guid CustomerId, Guid? RouteId,
    string PaymentType, decimal? TotalAmount, decimal? CodAmount,
    string SenderName, string SenderPhone, string SenderAddress,
    string ReceiverName, string ReceiverPhone, string ReceiverAddress,
    string? Remark, DateTime CreatedAt, Guid CreatedBy,
    IReadOnlyList<WaybillPackageDto> Packages);

public sealed record WaybillPackageDto(
    Guid Id, int PackageNo, string CargoName, int Quantity, decimal WeightKg, decimal VolumeM3, string? Remark);

public sealed record UpdateWaybillDraftRequest(
    string SenderName, string SenderPhone, string SenderAddress,
    string ReceiverName, string ReceiverPhone, string ReceiverAddress,
    string? Remark,
    IReadOnlyList<WaybillPackageDraft> Packages,
    decimal? DeclaredValue = null,
    /// <summary>重算所用价卡；null = 沿用最近一次计价快照的规则集。</summary>
    Guid? RuleSetId = null);

/// <summary>
/// 运单查询与草稿修改（WB-003）。数据范围：租户级范围 / 始发或目的网点可达（WB-PRINT 同语义）。
/// 草稿修改 = 快照字段 + 货物件整体替换（Draft 守卫在聚合）；需 waybill.manage + 起点网点范围。
/// </summary>
public interface IWaybillAdminService
{
    Task<CreateWaybillResult> CreateAsync(Guid operatorUserId, CreateWaybillCommand command);
    Task<PageResultWaybills> ListAsync(Guid operatorUserId, int page, int pageSize, string? waybillNo);
    Task<WaybillDetailDto> GetAsync(Guid operatorUserId, Guid waybillId);
    Task<WaybillDetailDto> UpdateDraftAsync(Guid operatorUserId, Guid waybillId, UpdateWaybillDraftRequest request);
    Task<WaybillDetailDto> ConfirmAsync(Guid operatorUserId, Guid waybillId);
    Task<WaybillDetailDto> CancelAsync(Guid operatorUserId, Guid waybillId, string? reason);

    /// <summary>运单事件时间线（WB-002；WB-003 查询端点）。</summary>
    Task<IReadOnlyList<WaybillEventDto>> ListEventsAsync(Guid operatorUserId, Guid waybillId);
}

public sealed record WaybillEventDto(
    Guid Id, string EventType, string? Detail, Guid? OperatorId, DateTime OccurredAt);

public sealed record PageResultWaybills(
    IReadOnlyList<WaybillListItemDto> Items, int Total, int Page, int PageSize);

public sealed class WaybillAdminService(
    WaybillsDbContext db,
    IAuthorizationService authorization,
    IWaybillService waybillService,
    IPricingTrialService pricingTrialService,
    IPricingSnapshotSerializer snapshotSerializer) : IWaybillAdminService
{
    public async Task<CreateWaybillResult> CreateAsync(Guid operatorUserId, CreateWaybillCommand command)
    {
        if (db.CurrentTenantId is null)
        {
            throw new TenantContextMissingException();
        }

        if (!await authorization.HasPermissionAsync(operatorUserId, WaybillPermissionCodes.WaybillManage))
        {
            throw new PermissionDeniedException(WaybillPermissionCodes.WaybillManage);
        }

        if (!await authorization.CanAccessBranchAsync(operatorUserId, command.OriginBranchId))
        {
            throw new PermissionDeniedException("运单数据范围（始发网点）");
        }

        return await waybillService.CreateWaybillAsync(command);
    }

    public async Task<PageResultWaybills> ListAsync(
        Guid operatorUserId, int page, int pageSize, string? waybillNo)
    {
        await RequireReadAsync(operatorUserId);
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var allowedBranchIds = await authorization.GetAuthorizedBranchIdsAsync(operatorUserId);
        if (allowedBranchIds.Count == 0)
        {
            return new PageResultWaybills([], 0, page, pageSize);
        }

        var query = db.Waybills.AsNoTracking()
            .Where(w => allowedBranchIds.Contains(w.OriginBranchId) || allowedBranchIds.Contains(w.DestBranchId));
        if (!string.IsNullOrWhiteSpace(waybillNo))
        {
            query = query.Where(w => w.WaybillNo.Contains(waybillNo.Trim()));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(w => w.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return new PageResultWaybills(
            items.Select(w => new WaybillListItemDto(
                w.Id, w.WaybillNo, w.Status.ToString(), w.CargoStatus.ToString(),
                w.TransportStatus.ToString(), w.DeliveryStatus.ToString(),
                w.FreightStatus.ToString(), w.CodStatus.ToString(),
                w.OriginBranchId, w.DestBranchId, w.TotalAmount, w.CreatedAt)).ToList(),
            total, page, pageSize);
    }

    public async Task<WaybillDetailDto> GetAsync(Guid operatorUserId, Guid waybillId)
    {
        var waybill = await RequireVisibleWaybillAsync(operatorUserId, waybillId);
        return MapDetail(waybill);
    }

    /// <summary>草稿修改：快照 + 货物件整体替换（聚合守卫仅 Draft 可改；确认后 409）。</summary>
    public async Task<WaybillDetailDto> UpdateDraftAsync(
        Guid operatorUserId, Guid waybillId, UpdateWaybillDraftRequest request)
    {
        if (!await authorization.HasPermissionAsync(operatorUserId, WaybillPermissionCodes.WaybillManage))
        {
            throw new PermissionDeniedException(WaybillPermissionCodes.WaybillManage);
        }

        var waybill = await db.Waybills
            .Include(w => w.Packages)
            .SingleOrDefaultAsync(w => w.Id == waybillId)
            ?? throw new EntityNotFoundException("运单", waybillId);

        // 数据范围：起点网点（草稿在始发网点维护）
        if (!await authorization.CanAccessBranchAsync(operatorUserId, waybill.OriginBranchId))
        {
            throw new PermissionDeniedException("运单数据范围（始发网点）");
        }

        // 聚合守卫：整体替换货物件（Draft）；快照字段更新。
        // Guid 主键非自动生成：显式设置实体状态（旧件 Deleted、新件 Added），EF 无法从导航推断
        var oldPackages = await db.Packages
            .Where(p => p.WaybillId == waybillId)
            .ToListAsync();
        waybill.UpdateDraft(
            request.SenderName, request.SenderPhone, request.SenderAddress,
            request.ReceiverName, request.ReceiverPhone, request.ReceiverAddress,
            request.Remark, request.Packages, DateTime.UtcNow, operatorUserId,
            declaredValue: request.DeclaredValue);

        // 草稿修改后重算价格（WB-002：RecalculatePricing 显式业务操作；新快照追加保留历史）
        var input = new PricingQuoteInput
        {
            WeightKg = request.Packages.Sum(p => p.WeightKg * p.Quantity),
            VolumeM3 = request.Packages.Sum(p => p.VolumeM3 * p.Quantity),
            PieceCount = request.Packages.Sum(p => p.Quantity),
            DeclaredValue = request.DeclaredValue,
            CustomerId = waybill.CustomerId,
            RouteId = waybill.RouteId,
            OriginBranchId = waybill.OriginBranchId,
            DestBranchId = waybill.DestBranchId,
        };
        PricingQuoteInput.Validate(input);
        var trial = await pricingTrialService.TrialCalculateAsync(
            request.RuleSetId ?? (await db.PricingSnapshots
                .Where(x => x.WaybillId == waybill.Id)
                .OrderByDescending(x => x.CreatedAt)
                .FirstAsync()).RuleSetId, input);
        waybill.RecalculatePricing(trial.Quote.Total, DateTime.UtcNow, operatorUserId);
        waybill.UpdateInsuranceFee(trial.Quote.InsuranceFee, DateTime.UtcNow, operatorUserId);
        db.PricingSnapshots.Add(WaybillPricingSnapshot.Create(
            waybill.TenantId, waybill.Id, trial.RuleSetId, trial.RuleSetCode,
            trial.VersionId, trial.VersionNumber,
            snapshotSerializer.SerializeInput(trial.Input),
            snapshotSerializer.SerializeQuote(trial.Quote),
            DateTime.UtcNow, operatorUserId));

        foreach (var oldPackage in oldPackages)
        {
            DbEntry(db, oldPackage).State = EntityState.Deleted;
        }

        foreach (var newPackage in waybill.Packages)
        {
            DbEntry(db, newPackage).State = EntityState.Added;
        }

        await db.SaveChangesAsync();

        return MapDetail(waybill);
    }

    public async Task<WaybillDetailDto> ConfirmAsync(Guid operatorUserId, Guid waybillId)
    {
        if (!await authorization.HasPermissionAsync(operatorUserId, WaybillPermissionCodes.WaybillManage))
        {
            throw new PermissionDeniedException(WaybillPermissionCodes.WaybillManage);
        }

        var waybill = await RequireVisibleWaybillAsync(operatorUserId, waybillId);
        waybill.Confirm(DateTime.UtcNow, operatorUserId);
        db.Events.Add(WaybillEvent.Create(
            waybill.TenantId, waybill.Id, WaybillEventType.Confirmed,
            WaybillStatus.Draft, WaybillStatus.Confirmed, DateTime.UtcNow,
            branchId: null, operatorId: operatorUserId, detail: null, createdBy: operatorUserId));
        await db.SaveChangesAsync();
        return MapDetail(waybill);
    }

    public async Task<WaybillDetailDto> CancelAsync(Guid operatorUserId, Guid waybillId, string? reason)
    {
        if (!await authorization.HasPermissionAsync(operatorUserId, WaybillPermissionCodes.WaybillManage))
        {
            throw new PermissionDeniedException(WaybillPermissionCodes.WaybillManage);
        }

        var waybill = await RequireVisibleWaybillAsync(operatorUserId, waybillId);
        waybill.Cancel(DateTime.UtcNow, operatorUserId);
        db.Events.Add(WaybillEvent.Create(
            waybill.TenantId, waybill.Id, WaybillEventType.Cancelled,
            WaybillStatus.Confirmed, WaybillStatus.Cancelled, DateTime.UtcNow,
            branchId: null, operatorId: operatorUserId,
            string.IsNullOrWhiteSpace(reason) ? null : $"取消原因：{reason.Trim()}", createdBy: operatorUserId));
        await db.SaveChangesAsync();
        return MapDetail(waybill);
    }

    public async Task<IReadOnlyList<WaybillEventDto>> ListEventsAsync(Guid operatorUserId, Guid waybillId)
    {
        await RequireReadAsync(operatorUserId);
        await RequireVisibleWaybillAsync(operatorUserId, waybillId);

        return (await db.Events
                .AsNoTracking()
                .Where(e => e.WaybillId == waybillId)
                .OrderBy(e => e.OccurredAt)
                .ToListAsync())
            .Select(e => new WaybillEventDto(
                e.Id, e.EventType.ToString(), e.Detail, e.CreatedBy, e.OccurredAt))
            .ToList();
    }

    // ---------- 辅助 ----------

    private async Task RequireReadAsync(Guid operatorUserId)
    {
        if (db.CurrentTenantId is null)
        {
            throw new TenantContextMissingException();
        }

        if (!await authorization.HasPermissionAsync(operatorUserId, WaybillPermissionCodes.WaybillRead))
        {
            throw new PermissionDeniedException(WaybillPermissionCodes.WaybillRead);
        }
    }

    /// <summary>可见性：租户级范围 / 始发或目的网点可达（WB-PRINT 同语义）。</summary>
    private async Task<Waybill> RequireVisibleWaybillAsync(Guid operatorUserId, Guid waybillId)
    {
        if (db.CurrentTenantId is null)
        {
            throw new TenantContextMissingException();
        }

        var waybill = await db.Waybills
            .Include(w => w.Packages)
            .SingleOrDefaultAsync(w => w.Id == waybillId)
            ?? throw new EntityNotFoundException("运单", waybillId);

        var tenantWide = (await db.Database
                .SqlQuery<TenantWideRow>(
                    $"""SELECT tenant_wide_scope AS "TenantWideScope" FROM users WHERE tenant_id = {db.CurrentTenantId} AND id = {operatorUserId}""")
                .ToListAsync())
            .SingleOrDefault() is { TenantWideScope: true };

        if (!tenantWide
            && !await authorization.CanAccessBranchAsync(operatorUserId, waybill.OriginBranchId)
            && !await authorization.CanAccessBranchAsync(operatorUserId, waybill.DestBranchId))
        {
            throw new PermissionDeniedException("运单数据范围（始发/目的网点）");
        }

        return waybill;
    }

    /// <summary>DbContext.Entry 扩展方法适配（调用点简洁）。</summary>
    private static Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<T> DbEntry<T>(
        WaybillsDbContext context, T entity) where T : class => context.Entry(entity);

    private sealed record TenantWideRow(bool TenantWideScope);

    private static WaybillDetailDto MapDetail(Waybill w) => new(
        w.Id, w.WaybillNo, w.Status, w.CargoStatus, w.TransportStatus, w.DeliveryStatus,
        w.FreightStatus, w.CodStatus, w.OriginBranchId, w.DestBranchId, w.CustomerId, w.RouteId,
        w.PaymentType.ToString(), w.TotalAmount, w.CodAmount,
        w.SenderName, w.SenderPhone, w.SenderAddress,
        w.ReceiverName, w.ReceiverPhone, w.ReceiverAddress, w.Remark, w.CreatedAt, w.CreatedBy ?? Guid.Empty,
        w.Packages.OrderBy(p => p.PackageNo).Select(p => new WaybillPackageDto(
            p.Id, p.PackageNo, p.CargoName, p.Quantity, p.WeightKg, p.VolumeM3, p.Remark)).ToList());
}
