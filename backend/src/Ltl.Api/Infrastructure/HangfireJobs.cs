using Ltl.Modules.Finance.Infrastructure;
using Ltl.Modules.Organization.Infrastructure;
using Ltl.Modules.Tenancy.Infrastructure;
using Ltl.Modules.Waybills.Infrastructure;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Ltl.Api.Infrastructure;

/// <summary>
/// Hangfire 后台任务（AGENTS §4.3：后台任务必须显式建立租户上下文，不得复用跨租户 DbContext）。
/// 遍历活跃租户时为每个租户创建独立 DI Scope 并显式调用 ITenantContext.SetTenant(tenantId)。
/// </summary>
public sealed class HangfireJobs(IServiceScopeFactory scopeFactory)
{
    /// <summary>清理过期刷新令牌（每日；按租户隔离执行）。</summary>
    public async Task CleanupExpiredTokensAsync()
    {
        var tenantIds = await ListActiveTenantIdsAsync();
        var totalDeleted = 0;

        foreach (var tenantId in tenantIds)
        {
            try
            {
                await using var tenantScope = scopeFactory.CreateAsyncScope();
                var tenantContext = tenantScope.ServiceProvider.GetRequiredService<ITenantContext>();
                tenantContext.SetTenant(tenantId);

                var db = tenantScope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
                var deleted = await db.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM refresh_tokens WHERE tenant_id = {tenantId} AND expires_at < NOW()");
                totalDeleted += deleted;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Hangfire] 清理过期刷新令牌失败：租户 {TenantId}", tenantId);
            }
        }

        Log.Information("[Hangfire] 清理过期刷新令牌：共删除 {Count} 条（覆盖 {TenantCount} 个租户）", totalDeleted, tenantIds.Count);
    }

    /// <summary>逾期应收检查（每日）：按租户独立建立上下文，统计超过账期未收款应收。</summary>
    public async Task CheckOverdueReceivablesAsync()
    {
        var tenantIds = await ListActiveTenantIdsAsync();

        foreach (var tenantId in tenantIds)
        {
            try
            {
                await using var tenantScope = scopeFactory.CreateAsyncScope();
                var tenantContext = tenantScope.ServiceProvider.GetRequiredService<ITenantContext>();
                tenantContext.SetTenant(tenantId);

                var db = tenantScope.ServiceProvider.GetRequiredService<FinanceDbContext>();
                var overdueCount = await db.Database
                    .SqlQuery<CountRow>($"""
                        SELECT COUNT(*) AS "Count" FROM receivables r
                        WHERE r.tenant_id = {tenantId}
                        AND r.status != 3
                        AND r.created_at < NOW() - INTERVAL '30 days'
                        """)
                    .ToListAsync();

                var count = overdueCount.SingleOrDefault()?.Count ?? 0;
                if (count > 0)
                {
                    Log.Warning("[Hangfire] 租户 {TenantId} 有 {Count} 笔逾期应收（超过 30 天）", tenantId, count);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Hangfire] 逾期应收检查失败：租户 {TenantId}", tenantId);
            }
        }
    }

    /// <summary>到货超时提醒（每日）：按租户独立建立上下文，检查已到货超过 3 天仍未交付的运单。</summary>
    public async Task CheckArrivalTimeoutAsync()
    {
        var tenantIds = await ListActiveTenantIdsAsync();

        foreach (var tenantId in tenantIds)
        {
            try
            {
                await using var tenantScope = scopeFactory.CreateAsyncScope();
                var tenantContext = tenantScope.ServiceProvider.GetRequiredService<ITenantContext>();
                tenantContext.SetTenant(tenantId);

                var db = tenantScope.ServiceProvider.GetRequiredService<WaybillsDbContext>();
                var timeoutCount = await db.Database
                    .SqlQuery<CountRow>($"""
                        SELECT COUNT(*) AS "Count" FROM waybills w
                        WHERE w.tenant_id = {tenantId}
                        AND w.delivery_status = 2
                        AND w.updated_at < NOW() - INTERVAL '3 days'
                        """)
                    .ToListAsync();

                var count = timeoutCount.SingleOrDefault()?.Count ?? 0;
                if (count > 0)
                {
                    Log.Warning("[Hangfire] 租户 {TenantId} 有 {Count} 张运单到货超过 3 天未交付", tenantId, count);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Hangfire] 到货超时检查失败：租户 {TenantId}", tenantId);
            }
        }
    }

    private async Task<List<Guid>> ListActiveTenantIdsAsync()
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var tenancyDb = scope.ServiceProvider.GetRequiredService<TenancyDbContext>();
        return (await tenancyDb.Database
                .SqlQuery<GuidRow>($"SELECT id FROM tenants WHERE status = 0")
                .ToListAsync())
            .Select(r => r.Id)
            .ToList();
    }

    private sealed record GuidRow(Guid Id);
    private sealed record CountRow(long Count);
}
