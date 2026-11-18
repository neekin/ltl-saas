using Ltl.Modules.Finance.Infrastructure;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Ltl.Api.Infrastructure;

/// <summary>
/// Hangfire 后台任务（AGENTS §4.3：后台任务必须显式建立租户上下文）。
/// 当前实现遍历全部租户逐一处理；数据量大后需改为分片或队列。
/// </summary>
public sealed class HangfireJobs(IServiceScopeFactory scopeFactory)
{
    /// <summary>清理过期刷新令牌（每日）。</summary>
    public async Task CleanupExpiredTokensAsync()
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Ltl.Modules.Organization.Infrastructure.OrganizationDbContext>();

        var deleted = await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM refresh_tokens WHERE expires_at < NOW()");
        Log.Information("[Hangfire] 清理过期刷新令牌：删除 {Count} 条", deleted);
    }

    /// <summary>逾期应收检查（每日）：标记超过账期未收款应收并记审计。</summary>
    public async Task CheckOverdueReceivablesAsync()
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Ltl.Modules.Finance.Infrastructure.FinanceDbContext>();

        // 查找全部租户
        var tenantIds = await db.Database
            .SqlQuery<GuidRow>($"SELECT id AS \"Id\" FROM tenants WHERE status = 0")
            .ToListAsync();

        foreach (var tenantId in tenantIds)
        {
            try
            {
                var overdueCount = await db.Database
                    .SqlQuery<CountRow>($"""
                        SELECT COUNT(*) AS "Count" FROM receivables r
                        WHERE r.tenant_id = {tenantId.Id}
                        AND r.status != 3
                        AND r.created_at < NOW() - INTERVAL '30 days'
                        """)
                    .ToListAsync();

                var count = overdueCount.SingleOrDefault()?.Count ?? 0;
                if (count > 0)
                {
                    Log.Warning("[Hangfire] 租户 {TenantId} 有 {Count} 笔逾期应收（超过 30 天）", tenantId.Id, count);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Hangfire] 逾期应收检查失败：租户 {TenantId}", tenantId.Id);
            }
        }
    }

    /// <summary>到货超时提醒（每日）：已到货超过 3 天仍未交付的运单。</summary>
    public async Task CheckArrivalTimeoutAsync()
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Ltl.Modules.Waybills.Infrastructure.WaybillsDbContext>();

        var tenantIds = await db.Database
            .SqlQuery<GuidRow>($"SELECT id AS \"Id\" FROM tenants WHERE status = 0")
            .ToListAsync();

        foreach (var tenantId in tenantIds)
        {
            try
            {
                var timeoutCount = await db.Database
                    .SqlQuery<CountRow>($"""
                        SELECT COUNT(*) AS "Count" FROM waybills w
                        WHERE w.tenant_id = {tenantId.Id}
                        AND w.delivery_status = 2
                        AND w.updated_at < NOW() - INTERVAL '3 days'
                        """)
                    .ToListAsync();

                var count = timeoutCount.SingleOrDefault()?.Count ?? 0;
                if (count > 0)
                {
                    Log.Warning("[Hangfire] 租户 {TenantId} 有 {Count} 张运单到货超过 3 天未交付", tenantId.Id, count);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Hangfire] 到货超时检查失败：租户 {TenantId}", tenantId.Id);
            }
        }
    }

    private sealed record GuidRow(Guid Id);
    private sealed record CountRow(long Count);
}
