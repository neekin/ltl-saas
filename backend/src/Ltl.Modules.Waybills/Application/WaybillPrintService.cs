using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Waybills.Domain;
using Ltl.Modules.Waybills.Infrastructure;
using Ltl.SharedKernel;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Waybills.Application;

public sealed record WaybillPrintPackageDto(
    int PackageNo, string CargoName, int Quantity, decimal WeightKg, decimal VolumeM3, string? Remark);

/// <summary>运单打印视图（只读，WB-PRINT）：打印模板数据源。</summary>
public sealed record WaybillPrintDto(
    Guid WaybillId,
    string WaybillNo,
    string WaybillStatus,
    string CargoStatus,
    string TransportStatus,
    string DeliveryStatus,
    string FreightStatus,
    string CodStatus,
    string PaymentType,
    string SenderName, string SenderPhone, string SenderAddress,
    string ReceiverName, string ReceiverPhone, string ReceiverAddress,
    decimal? TotalAmount, decimal? CodAmount,
    IReadOnlyList<WaybillPrintPackageDto> Packages,
    int DeclaredPieces, decimal DeclaredWeightKg, decimal DeclaredVolumeM3,
    DateTime CreatedAt, Guid? CreatedBy);

public interface IWaybillPrintService
{
    /// <summary>打印数据（waybill.read 权限 + 数据范围：租户级范围 / 始发或目的网点可达）。</summary>
    Task<WaybillPrintDto> GetPrintViewAsync(Guid operatorUserId, Guid waybillId);
}

/// <summary>打印数据实现（只读）。数据范围实时校验（ORG-002 语义）。</summary>
public sealed class WaybillPrintService(
    WaybillsDbContext db,
    IAuthorizationService authorization) : IWaybillPrintService
{
    public async Task<WaybillPrintDto> GetPrintViewAsync(Guid operatorUserId, Guid waybillId)
    {
        // 先取实体：跨租户 Id → 过滤器不可见 → 404（优先于权限/范围语义）
        var waybill = await db.Waybills
            .Include(w => w.Packages)
            .SingleOrDefaultAsync(w => w.Id == waybillId)
            ?? throw new EntityNotFoundException("运单", waybillId);

        if (!await authorization.HasPermissionAsync(operatorUserId, WaybillPermissionCodes.WaybillRead))
        {
            throw new PermissionDeniedException(WaybillPermissionCodes.WaybillRead);
        }

        var tenantId = db.CurrentTenantId ?? throw new TenantContextMissingException();

        // 数据范围：租户级范围 或 始发/目的网点可达（AGENTS §5.2）。
        // 租户级范围经原生 SQL 显式租户参数（users 为 Organization 域表，AGENTS §5.1 原生 SQL 约定）
        var tenantWide = (await db.Database
                .SqlQuery<TenantWideRow>(
                    $"""SELECT tenant_wide_scope AS "TenantWideScope" FROM users WHERE tenant_id = {tenantId} AND id = {operatorUserId}""")
                .ToListAsync())
            .SingleOrDefault() is { TenantWideScope: true };
        var scopeGranted = tenantWide
            || await authorization.CanAccessBranchAsync(operatorUserId, waybill.OriginBranchId)
            || await authorization.CanAccessBranchAsync(operatorUserId, waybill.DestBranchId);
        if (!scopeGranted)
        {
            throw new PermissionDeniedException("运单数据范围（始发/目的网点）");
        }

        return new WaybillPrintDto(
            waybill.Id, waybill.WaybillNo,
            waybill.Status.ToString(), waybill.CargoStatus.ToString(),
            waybill.TransportStatus.ToString(), waybill.DeliveryStatus.ToString(),
            waybill.FreightStatus.ToString(), waybill.CodStatus.ToString(),
            waybill.PaymentType.ToString(),
            waybill.SenderName, waybill.SenderPhone, waybill.SenderAddress,
            waybill.ReceiverName, waybill.ReceiverPhone, waybill.ReceiverAddress,
            waybill.TotalAmount, waybill.CodAmount,
            waybill.Packages.OrderBy(p => p.PackageNo).Select(p =>
                new WaybillPrintPackageDto(
                    p.PackageNo, p.CargoName, p.Quantity, p.WeightKg, p.VolumeM3, p.Remark)).ToList(),
            waybill.TotalPieces, waybill.TotalWeightKg, waybill.TotalVolumeM3,
            waybill.CreatedAt, waybill.CreatedBy);
    }

    private sealed record TenantWideRow(bool TenantWideScope);
}
