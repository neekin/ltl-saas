namespace Ltl.Modules.Waybills.Domain;

/// <summary>
/// 计价快照（追加式，AGENTS §6.4 / WB-002 设计 §3）：开单时的规则版本引用、输入快照与
/// 逐项费用明细快照，保证历史可解释、可复现。不更新、不删除；重新计价追加新快照（后续任务）。
/// 规则集/版本以**快照值**存储，不建跨模块外键（与 PRICE-001 §4 决策一致）。
/// input/quote 为 JSONB（经 Pricing.IPricingSnapshotSerializer 的强类型选项序列化）。
/// </summary>
public sealed class WaybillPricingSnapshot : TenantScopedEntity
{
    public Guid WaybillId { get; private set; }

    public Guid RuleSetId { get; private set; }

    /// <summary>规则集编码（快照值，规则集编码可能后续变更）。</summary>
    public string RuleSetCode { get; private set; } = null!;

    /// <summary>命中版本 Id（PRICE-002 执行器按版本定义计算）。</summary>
    public Guid VersionId { get; private set; }

    public int VersionNumber { get; private set; }

    /// <summary>输入快照 JSONB。</summary>
    public string InputJson { get; private set; } = null!;

    /// <summary>计价结果快照 JSONB（含逐项明细解释）。</summary>
    public string QuoteJson { get; private set; } = null!;

    private WaybillPricingSnapshot()
    {
    }

    internal static WaybillPricingSnapshot Create(
        Guid tenantId,
        Guid waybillId,
        Guid ruleSetId,
        string ruleSetCode,
        Guid versionId,
        int versionNumber,
        string inputJson,
        string quoteJson,
        DateTime createdAtUtc,
        Guid? createdBy = null)
    {
        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("时间必须为 UTC。", nameof(createdAtUtc));
        }

        return new WaybillPricingSnapshot
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            WaybillId = waybillId,
            RuleSetId = ruleSetId,
            RuleSetCode = RequireNonBlank(ruleSetCode, "规则集编码", 64),
            VersionId = versionId,
            VersionNumber = versionNumber,
            InputJson = inputJson,
            QuoteJson = quoteJson,
            CreatedAt = createdAtUtc,
            CreatedBy = createdBy,
        };
    }
}
