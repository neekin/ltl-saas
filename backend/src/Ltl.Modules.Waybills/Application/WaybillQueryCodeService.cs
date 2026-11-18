using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Waybills.Domain;
using Ltl.Modules.Waybills.Infrastructure;
using Ltl.SharedKernel;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Waybills.Application;

/// <summary>客户可见的运单查询视图（P10）：仅单票脱敏信息，不含内部 ID/网点/客户/操作人。</summary>
public sealed record CustomerWaybillView(
    string WaybillNo,
    string Status,
    string CargoStatus,
    string TransportStatus,
    string DeliveryStatus,
    string FreightStatus,
    string SenderName,
    string SenderPhoneMasked,
    string ReceiverName,
    string ReceiverPhoneMasked,
    decimal? TotalAmount,
    DateTime CreatedAt,
    IReadOnlyList<CustomerPackageView> Packages);

public sealed record CustomerPackageView(
    int PackageNo, string CargoName, int Quantity, decimal WeightKg, decimal VolumeM3);

/// <summary>
/// 客户查询码服务（P10）：查询码哈希存储；匿名查询必须租户编码 + 运单号 + 查询码同时命中；
/// 内部重置需 waybill.read 权限（数据范围同 WaybillAdminService）。
/// </summary>
public interface IWaybillQueryCodeService
{
    /// <summary>匿名客户查询：租户编码 + 运单号 + 查询码，命中返回脱敏视图，否则抛未授权。</summary>
    Task<CustomerWaybillView> QueryAsync(string tenantCode, string waybillNo, string queryCode);

    /// <summary>内部重置查询码（waybill.read + 网点范围），返回新明文码。</summary>
    Task<string> ResetAsync(Guid operatorUserId, Guid waybillId);
}

public sealed class WaybillQueryCodeService(
    WaybillsDbContext db,
    ITenantContext tenantContext,
    IAuthorizationService authorization) : IWaybillQueryCodeService
{
    public async Task<CustomerWaybillView> QueryAsync(string tenantCode, string waybillNo, string queryCode)
    {
        // 匿名端点暴露在公网：空 body / 缺字段等畸形请求统一按"凭据错误"拒绝（401），
        // 既避免对 null 解引用产生 500，也不让调用方区分"参数缺失"与"凭据错误"（防枚举，AGENTS §8）。
        if (string.IsNullOrWhiteSpace(tenantCode) ||
            string.IsNullOrWhiteSpace(waybillNo) ||
            string.IsNullOrWhiteSpace(queryCode))
        {
            throw new UnauthorizedAccessException("运单号或查询码不正确");
        }

        // 匿名端点无 JWT，不能用 ITenantContext；由租户编码解析租户 ID（原生 SQL 显式参数）。
        // tenantCode 仅用于定位租户，授权依据是运单号 + 查询码双因子（AGENTS §5.1）。
        var code = tenantCode.Trim();
        var tenantRow = (await db.Database
            .SqlQuery<TenantLookupRow>($"""SELECT id AS "Id" FROM tenants WHERE code = {code} AND status = 0""")
            .ToListAsync())
            .SingleOrDefault();
        var tenantId = tenantRow?.Id ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("运单号或查询码不正确");
        }

        // 匿名请求无 JWT → ITenantContext 为空 → 全局查询过滤器会过滤掉所有运单。
        // 因此显式 IgnoreQueryFilters 并在查询条件中强制 tenant_id（AGENTS §5.1：范围检查不依赖过滤器）。
        var waybill = await db.Waybills
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(w => w.TenantId == tenantId && w.WaybillNo == waybillNo);
        if (waybill is null || !waybill.VerifyQueryCode(queryCode))
        {
            // 不区分"运单不存在"与"查询码错误"，防枚举（AGENTS §8）。
            throw new UnauthorizedAccessException("运单号或查询码不正确");
        }

        var packages = await db.Packages
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && p.WaybillId == waybill.Id)
            .OrderBy(p => p.PackageNo)
            .ToListAsync();

        return new CustomerWaybillView(
            waybill.WaybillNo,
            waybill.Status.ToString(),
            waybill.CargoStatus.ToString(),
            waybill.TransportStatus.ToString(),
            waybill.DeliveryStatus.ToString(),
            waybill.FreightStatus.ToString(),
            waybill.SenderName,
            MaskPhone(waybill.SenderPhone),
            waybill.ReceiverName,
            MaskPhone(waybill.ReceiverPhone),
            waybill.TotalAmount,
            waybill.CreatedAt,
            packages
                .Select(p => new CustomerPackageView(
                    p.PackageNo, p.CargoName, p.Quantity, p.WeightKg, p.VolumeM3))
                .ToList());
    }

    public async Task<string> ResetAsync(Guid operatorUserId, Guid waybillId)
    {
        if (tenantContext.TenantId is not { } tenantId)
            throw new TenantContextMissingException();
        if (!await authorization.HasPermissionAsync(operatorUserId, "waybill.read"))
            throw new PermissionDeniedException("waybill.read");

        var waybill = await db.Waybills.SingleOrDefaultAsync(w => w.TenantId == tenantId && w.Id == waybillId)
            ?? throw new EntityNotFoundException("运单", waybillId);
        var code = Waybill.GenerateQueryCode();
        waybill.SetQueryCode(code, DateTime.UtcNow, operatorUserId);
        await db.SaveChangesAsync();
        return code;
    }

    private sealed record TenantLookupRow(Guid Id);

    /// <summary>手机号脱敏：保留前 3 后 4 位。</summary>
    private static string MaskPhone(string phone)
    {
        if (string.IsNullOrEmpty(phone)) return string.Empty;
        if (phone.Length <= 7) return new string('*', phone.Length);
        return $"{phone[..3]}{new string('*', phone.Length - 7)}{phone[^4..]}";
    }
}
