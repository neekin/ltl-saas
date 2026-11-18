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

/// <summary>维度报表行：网点/线路/客户维度的聚合指标（REPORT-002）。</summary>
public sealed record DimensionMetric(
    Guid Id,
    string Code,
    string Name,
    int WaybillCount,
    decimal FreightRevenue,
    decimal TotalWeightKg,
    decimal TotalVolumeM3);

/// <summary>租户用量与配额（TEN-002：Max 为 null = 不限制）。</summary>
public sealed record TenantUsage(
    int WaybillCount,
    int UserCount,
    int FileCount,
    long StorageBytes,
    string? PlanName,
    int? MaxWaybills,
    int? MaxUsers,
    long? MaxStorageBytes);

public interface IReportingService
{
    /// <summary>经营看板指标（租户级聚合）。month 可选（yyyyMM 格式，筛选指定月份数据）。</summary>
    Task<DashboardMetrics> GetDashboardAsync(Guid operatorUserId, string? month);

    /// <summary>租户用量统计（当前租户；需 finance.read）。</summary>
    Task<TenantUsage> GetTenantUsageAsync(Guid operatorUserId);

    /// <summary>网点维度报表：按运单始发网点聚合。</summary>
    Task<IReadOnlyList<DimensionMetric>> GetBranchReportAsync(Guid operatorUserId, string? month);

    /// <summary>线路维度报表：按运单关联线路聚合（未指定线路的运单单列一行）。</summary>
    Task<IReadOnlyList<DimensionMetric>> GetRouteReportAsync(Guid operatorUserId, string? month);

    /// <summary>客户维度报表：按运单客户聚合。</summary>
    Task<IReadOnlyList<DimensionMetric>> GetCustomerReportAsync(Guid operatorUserId, string? month);
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

        // month 为 yyyyMM 时按指定月份筛选，否则按当前月（AGENTS §7 时间按 Asia/Shanghai 展示，此处按 UTC 存储口径聚合）。
        var (periodStart, periodEnd) = ResolveMonthRange(month);

        var row = (await db.Database
            .SqlQuery<DashboardRawRow>($"""
                SELECT
                    (SELECT COUNT(*) FROM waybills w WHERE w.tenant_id = {tenantId} AND w.waybill_status != 3) AS "total_waybills",
                    (SELECT COUNT(*) FROM waybills w WHERE w.tenant_id = {tenantId} AND w.waybill_status != 3 AND w.created_at >= {periodStart} AND w.created_at < {periodEnd}) AS "monthly_waybills",
                    (SELECT COALESCE(SUM(wp.weight_kg * wp.quantity), 0) FROM waybill_packages wp JOIN waybills w ON w.tenant_id = wp.tenant_id AND w.id = wp.waybill_id WHERE wp.tenant_id = {tenantId} AND w.waybill_status != 3) AS "total_cargo_weight_kg",
                    (SELECT COALESCE(SUM(wp.volume_m3 * wp.quantity), 0) FROM waybill_packages wp JOIN waybills w ON w.tenant_id = wp.tenant_id AND w.id = wp.waybill_id WHERE wp.tenant_id = {tenantId} AND w.waybill_status != 3) AS "total_cargo_volume_m3",
                    (SELECT COALESCE(SUM(w.total_amount), 0) FROM waybills w WHERE w.tenant_id = {tenantId} AND w.waybill_status != 3 AND w.total_amount IS NOT NULL) AS "total_freight_revenue",
                    (SELECT COALESCE(SUM(r.amount), 0) FROM receivables r WHERE r.tenant_id = {tenantId}) AS "total_receivable_amount",
                    (SELECT COALESCE(SUM(r.settled_amount), 0) FROM receivables r WHERE r.tenant_id = {tenantId}) AS "total_settled_amount",
                    (SELECT COALESCE(SUM(r.amount - r.settled_amount), 0) FROM receivables r WHERE r.tenant_id = {tenantId}) AS "total_outstanding_amount",
                    (SELECT COALESCE(SUM(c.collected_amount), 0) FROM cod_orders c WHERE c.tenant_id = {tenantId}) AS "total_cod_collected",
                    (SELECT COALESCE(SUM(c.paid_amount), 0) FROM cod_orders c WHERE c.tenant_id = {tenantId}) AS "total_cod_paid_out",
                    (SELECT COUNT(*) FROM branches b WHERE b.tenant_id = {tenantId} AND b.status = 0) AS "active_branch_count",
                    (SELECT COUNT(*) FROM customers c WHERE c.tenant_id = {tenantId} AND c.status = 0) AS "active_customer_count"
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

    public async Task<TenantUsage> GetTenantUsageAsync(Guid operatorUserId)
    {
        if (tenantContext.TenantId is not { } tenantId)
            throw new TenantContextMissingException();
        if (!await authorization.HasPermissionAsync(operatorUserId, "finance.read"))
            throw new PermissionDeniedException("finance.read");

        var row = (await db.Database
            .SqlQuery<UsageRawRow>($"""
                SELECT
                    (SELECT COUNT(*) FROM waybills WHERE tenant_id = {tenantId}) AS "waybill_count",
                    (SELECT COUNT(*) FROM users WHERE tenant_id = {tenantId}) AS "user_count",
                    (SELECT COUNT(*) FROM file_objects WHERE tenant_id = {tenantId}) AS "file_count",
                    (SELECT COALESCE(SUM(size_bytes), 0) FROM file_objects WHERE tenant_id = {tenantId}) AS "storage_bytes",
                    (SELECT plan_name FROM tenants WHERE id = {tenantId}) AS "plan_name",
                    (SELECT max_waybills FROM tenants WHERE id = {tenantId}) AS "max_waybills",
                    (SELECT max_users FROM tenants WHERE id = {tenantId}) AS "max_users",
                    (SELECT max_storage_bytes FROM tenants WHERE id = {tenantId}) AS "max_storage_bytes"
                """)
            .ToListAsync())
            .SingleOrDefault() ?? new UsageRawRow(0, 0, 0, 0, null, null, null, null);

        return new TenantUsage((int)row.WaybillCount, (int)row.UserCount, (int)row.FileCount, row.StorageBytes,
            row.PlanName, row.MaxWaybills, row.MaxUsers, row.MaxStorageBytes);
    }

    public async Task<IReadOnlyList<DimensionMetric>> GetBranchReportAsync(Guid operatorUserId, string? month)
    {
        if (tenantContext.TenantId is not { } tenantId)
            throw new TenantContextMissingException();
        if (!await authorization.HasPermissionAsync(operatorUserId, "finance.read"))
            throw new PermissionDeniedException("finance.read");

        var (start, end) = ResolveMonthRange(month);
        return await db.Database
            .SqlQuery<DimensionMetric>($"""
                SELECT b.id AS "id", b.code AS "code", b.name AS "name",
                       COUNT(w.id)::integer AS "waybill_count",
                       COALESCE(SUM(w.total_amount), 0) AS "freight_revenue",
                       COALESCE(SUM(p.line_weight), 0) AS "total_weight_kg",
                       COALESCE(SUM(p.line_volume), 0) AS "total_volume_m3"
                FROM branches b
                LEFT JOIN waybills w ON w.tenant_id = b.tenant_id AND w.origin_branch_id = b.id
                    AND w.waybill_status != 3 AND w.created_at >= {start} AND w.created_at < {end}
                LEFT JOIN LATERAL (
                    SELECT COALESCE(SUM(wp.weight_kg * wp.quantity), 0) AS line_weight,
                           COALESCE(SUM(wp.volume_m3 * wp.quantity), 0) AS line_volume
                    FROM waybill_packages wp
                    WHERE wp.tenant_id = w.tenant_id AND wp.waybill_id = w.id
                ) p ON TRUE
                WHERE b.tenant_id = {tenantId}
                GROUP BY b.id, b.code, b.name
                ORDER BY "waybill_count" DESC, b.code
                """)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<DimensionMetric>> GetRouteReportAsync(Guid operatorUserId, string? month)
    {
        if (tenantContext.TenantId is not { } tenantId)
            throw new TenantContextMissingException();
        if (!await authorization.HasPermissionAsync(operatorUserId, "finance.read"))
            throw new PermissionDeniedException("finance.read");

        var (start, end) = ResolveMonthRange(month);
        return await db.Database
            .SqlQuery<DimensionMetric>($"""
                SELECT r.id AS "id", r.code AS "code", r.name AS "name",
                       COUNT(w.id)::integer AS "waybill_count",
                       COALESCE(SUM(w.total_amount), 0) AS "freight_revenue",
                       COALESCE(SUM(p.line_weight), 0) AS "total_weight_kg",
                       COALESCE(SUM(p.line_volume), 0) AS "total_volume_m3"
                FROM routes r
                LEFT JOIN waybills w ON w.tenant_id = r.tenant_id AND w.route_id = r.id
                    AND w.waybill_status != 3 AND w.created_at >= {start} AND w.created_at < {end}
                LEFT JOIN LATERAL (
                    SELECT COALESCE(SUM(wp.weight_kg * wp.quantity), 0) AS line_weight,
                           COALESCE(SUM(wp.volume_m3 * wp.quantity), 0) AS line_volume
                    FROM waybill_packages wp
                    WHERE wp.tenant_id = w.tenant_id AND wp.waybill_id = w.id
                ) p ON TRUE
                WHERE r.tenant_id = {tenantId}
                GROUP BY r.id, r.code, r.name
                ORDER BY "waybill_count" DESC, r.code
                """)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<DimensionMetric>> GetCustomerReportAsync(Guid operatorUserId, string? month)
    {
        if (tenantContext.TenantId is not { } tenantId)
            throw new TenantContextMissingException();
        if (!await authorization.HasPermissionAsync(operatorUserId, "finance.read"))
            throw new PermissionDeniedException("finance.read");

        var (start, end) = ResolveMonthRange(month);
        return await db.Database
            .SqlQuery<DimensionMetric>($"""
                SELECT c.id AS "id", c.code AS "code", c.name AS "name",
                       COUNT(w.id)::integer AS "waybill_count",
                       COALESCE(SUM(w.total_amount), 0) AS "freight_revenue",
                       COALESCE(SUM(p.line_weight), 0) AS "total_weight_kg",
                       COALESCE(SUM(p.line_volume), 0) AS "total_volume_m3"
                FROM customers c
                LEFT JOIN waybills w ON w.tenant_id = c.tenant_id AND w.customer_id = c.id
                    AND w.waybill_status != 3 AND w.created_at >= {start} AND w.created_at < {end}
                LEFT JOIN LATERAL (
                    SELECT COALESCE(SUM(wp.weight_kg * wp.quantity), 0) AS line_weight,
                           COALESCE(SUM(wp.volume_m3 * wp.quantity), 0) AS line_volume
                    FROM waybill_packages wp
                    WHERE wp.tenant_id = w.tenant_id AND wp.waybill_id = w.id
                ) p ON TRUE
                WHERE c.tenant_id = {tenantId}
                GROUP BY c.id, c.code, c.name
                ORDER BY "waybill_count" DESC, c.code
                """)
            .ToListAsync();
    }

    /// <summary>解析 yyyyMM 为 UTC 月初/次月初区间；非法或空值回退为当前月。</summary>
    private static (DateTime Start, DateTime End) ResolveMonthRange(string? month)
    {
        var now = DateTime.UtcNow;
        var year = now.Year;
        var monthNum = now.Month;
        if (month is { Length: 6 } && int.TryParse(month[..4], out var y) && int.TryParse(month[4..], out var m)
            && y >= 2000 && y <= 9999 && m >= 1 && m <= 12)
        {
            year = y;
            monthNum = m;
        }
        var start = new DateTime(year, monthNum, 1, 0, 0, 0, DateTimeKind.Utc);
        return (start, start.AddMonths(1));
    }

    private sealed record UsageRawRow(
        long WaybillCount, long UserCount, long FileCount, long StorageBytes,
        string? PlanName, int? MaxWaybills, int? MaxUsers, long? MaxStorageBytes);

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
