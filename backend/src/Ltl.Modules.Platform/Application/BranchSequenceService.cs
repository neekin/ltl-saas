using Ltl.Modules.Platform.Domain;
using Ltl.Modules.Platform.Infrastructure;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Platform.Application;

/// <summary>
/// 序号分配实现：单条原生 SQL（INSERT ... ON CONFLICT ... DO UPDATE ... RETURNING）原子完成
/// "初始化或递增"，行锁保证并发无重复（SEQ-001 设计文档第 2 节）。
/// 租户参数显式携带于 SQL（不依赖过滤器兜底）；无租户上下文 → 拒绝（fail-closed）。
/// </summary>
public sealed class BranchSequenceService(PlatformDbContext db) : IBranchSequenceService
{
    public async Task<string> NextNumberAsync(
        Guid branchId,
        BusinessDocumentType docType,
        DateOnly date,
        string branchCode)
    {
        if (db.CurrentTenantId is not { } tenantId)
        {
            throw new TenantContextMissingException();
        }

        if (branchId == Guid.Empty)
        {
            throw new ArgumentException("网点 Id 不能为空 Guid。", nameof(branchId));
        }

        var period = BusinessNumberFormatter.Period(date);

        // INSERT...RETURNING 不可组合（不能作子查询），ToListAsync 原样执行后再在内存取单行；
        // SingleAsync 会被 EF 包装成 SELECT 子查询导致生成非法 SQL。
        var sequence = (await db.Database
            .SqlQuery<long>($"""
                INSERT INTO branch_sequences (tenant_id, branch_id, doc_type, period, current_value, updated_at)
                VALUES ({tenantId}, {branchId}, {(short)docType}, {period}, 1, now())
                ON CONFLICT (tenant_id, branch_id, doc_type, period)
                DO UPDATE SET current_value = branch_sequences.current_value + 1, updated_at = now()
                RETURNING current_value AS "Value"
                """)
            .ToListAsync())
            .Single();

        return BusinessNumberFormatter.Format(docType, branchCode, date, sequence);
    }
}
