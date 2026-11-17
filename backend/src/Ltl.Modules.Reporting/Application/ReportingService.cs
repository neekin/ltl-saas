using Ltl.Modules.Finance.Infrastructure;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.SharedKernel.Application;
using Ltl.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Reporting.Application;

public sealed record DashboardMetrics(
    int TotalWaybills,
    int MonthlyWaybills,
    decimal TotalCargoWeightKg,
    decimal TotalCargoVolumeM3,
    decimal TotalFreightRevenue,
    decimal TotalReceivableAmount,
    decimal TotalSettledAmount,
    decimal TotalOutstandingAmount,
    decimal TotalCodCollected,
    decimal TotalCodPaidOut,
    int ActiveBranchCount,
    int ActiveCustomerCount);

public interface IReportingService
{
    /// <summary>经营看板指标（租户级聚合）。month 可选（yyyyMM 格式，筛选本月数据）。</summary>
    Task<DashboardMetrics> GetDashboardAsync(Guid operatorUserId, string? month);
}

/// <summary>
/// 经营看板聚合查询（REPORT-001）：原生 SQL 跨模块聚合（AGENTS §5.1）。
/// 只读端点；权限：finance.read。使用 FinanceDbContext 连接（同一数据库）。
/// </summary>
public sealed class ReportingService(
    FinanceDbContext db,
    ITenantContext tenantContext,
    IAuthorizationService authorization) : IReportingService
{
    public async Task<DashboardMetrics> GetDashboardAsync(Guid operatorUserId, string? month)
    {
        if (tenantContext.TenantId is not { } tenantId)
            throw new TenantContextMissingException();

        if (!await authorization.HasPermissionAsync(operatorUserId, "finance.read"))
            throw new PermissionDeniedException("finance.read");

        var row = (await db.Database
            .SqlQuery<DashboardRawRow>($"""
                SELECT
                    (SELECT COUNT(*) FROM waybills w WHERE w.tenant_id = {tenantId} AND w.status != 3) AS "TotalWaybills",
                    (SELECT COUNT(*) FROM waybills w WHERE w.tenant_id = {tenantId} AND w.status != 3 AND DATE_TRUNC('month', w.created_at) = DATE_TRUNC('month', NOW())) AS "MonthlyWaybills",
                    (SELECT COALESCE(SUM(wp.weight_kg * wp.quantity), 0) FROM waybill_packages wp JOIN waybills w ON w.tenant_id = wp.tenant_id AND w.id = wp.waybill_id WHERE wp.tenant_id = {tenantId} AND w.status != 3) AS "TotalCargoWeightKg",
                    (SELECT COALESCE(SUM(wp.volume_m3 * wp.quantity), 0) FROM waybill_packages wp JOIN waybills w ON w.tenant_id = wp.tenant_id AND w.id = wp.waybill_id WHERE wp.tenant_id = {tenantId} AND w.status != 3) AS "TotalCargoVolumeM3",
                    (SELECT COALESCE(SUM(w.total_amount), 0) FROM waybills w WHERE w.tenant_id = {tenantId} AND w.status != 3 AND w.total_amount IS NOT NULL) AS "TotalFreightRevenue",
                    (SELECT COALESCE(SUM(r.amount), 0) FROM receivables r WHERE r.tenant_id = {tenantId}) AS "TotalReceivableAmount",
                    (SELECT COALESCE(SUM(r.settled_amount), 0) FROM receivables r WHERE r.tenant_id = {tenantId}) AS "TotalSettledAmount",
                    (SELECT COALESCE(SUM(r.amount - r.settled_amount), 0) FROM receivables r WHERE r.tenant_id = {tenantId}) AS "TotalOutstandingAmount",
                    (SELECT COALESCE(SUM(c.collected_amount), 0) FROM cod_orders c WHERE c.tenant_id = {tenantId}) AS "TotalCodCollected",
                    (SELECT COALESCE(SUM(c.paid_amount), 0) FROM cod_orders c WHERE c.tenant_id = {tenantId}) AS "TotalCodPaidOut",
                    (SELECT COUNT(*) FROM branches b WHERE b.tenant_id = {tenantId} AND b.status = 0) AS "ActiveBranchCount",
                    (SELECT COUNT(*) FROM customers c WHERE c.tenant_id = {tenantId} AND c.status = 0) AS "ActiveCustomerCount"
                """)
            .ToListAsync())
            .SingleOrDefault()
            ?? new DashboardRawRow(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        return new DashboardMetrics(
            (int)row.TotalWaybills,
            (int)row.MonthlyWaybills,
            row.TotalCargoWeightKg,
            row.TotalCargoVolumeM3,
            row.TotalFreightRevenue,
            row.TotalReceivableAmount,
            row.TotalSettledAmount,
            row.TotalOutstandingAmount,
            row.TotalCodCollected,
            row.TotalCodPaidOut,
            (int)row.ActiveBranchCount,
            (int)row.ActiveCustomerCount);
    }

    private sealed record DashboardRawRow(
        long TotalWaybills,
        long MonthlyWaybills,
        decimal TotalCargoWeightKg,
        decimal TotalCargoVolumeM3,
        decimal TotalFreightRevenue,
        decimal TotalReceivableAmount,
        decimal TotalSettledAmount,
        decimal TotalOutstandingAmount,
        decimal TotalCodCollected,
        decimal TotalCodPaidOut,
        long ActiveBranchCount,
        long ActiveCustomerCount);
}
