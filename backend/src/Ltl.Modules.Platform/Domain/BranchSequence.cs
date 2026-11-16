using Ltl.SharedKernel;

namespace Ltl.Modules.Platform.Domain;

/// <summary>
/// 网点业务序号计数行（branch_sequences）：按（租户、网点、单据类型、期间）维度计数。
/// 分配走原生 SQL 原子 upsert+RETURNING（SEQ-001 设计文档第 2 节），不经过本实体的普通 SaveChanges 写入。
/// </summary>
public sealed class BranchSequence : ITenantScopedEntity
{
    public Guid TenantId { get; private set; }

    public Guid BranchId { get; private set; }

    public BusinessDocumentType DocType { get; private set; }

    /// <summary>归属期间 yyyyMMdd（按日重置）。</summary>
    public string Period { get; private set; } = null!;

    public long CurrentValue { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private BranchSequence()
    {
    }
}
