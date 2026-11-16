namespace Ltl.Modules.Pricing.Domain;

/// <summary>
/// 规则集版本（ADR-0007）：Draft 可编辑；Published 为不可变 JSONB 快照；
/// 新版本发布后旧 Published → Superseded，历史永不删除。
/// </summary>
public sealed class PricingRuleSetVersion : TenantScopedEntity
{
    public Guid RuleSetId { get; private set; }

    /// <summary>版本号（集合内递增，≥1；创建草稿时分配，唯一索引兜底）。</summary>
    public int VersionNumber { get; private set; }

    public PricingVersionStatus Status { get; private set; }

    /// <summary>定义快照（结构 + 语义校验通过后写入；已发布版本不可变）。</summary>
    public PricingRuleDefinition Definition { get; private set; } = null!;

    public DateTime? PublishedAt { get; private set; }

    public Guid? PublishedBy { get; private set; }

    private PricingRuleSetVersion()
    {
    }

    /// <summary>创建草稿。定义先经语义校验（零错误），避免无效定义入库。</summary>
    /// <exception cref="ArgumentException">参数非法时抛出。</exception>
    /// <exception cref="PricingRuleViolationException">定义校验存在错误时抛出。</exception>
    public static PricingRuleSetVersion CreateDraft(
        Guid tenantId,
        Guid ruleSetId,
        int versionNumber,
        PricingRuleDefinition definition,
        DateTime nowUtc,
        Guid? createdBy = null)
    {
        var errors = PricingDefinitionValidator.Validate(definition);
        if (errors.Count > 0)
        {
            throw new PricingRuleViolationException(
                $"规则定义校验未通过（{errors.Count} 项）：{string.Join("；", errors)}");
        }

        if (versionNumber < 1)
        {
            throw new ArgumentException("版本号必须 ≥1。", nameof(versionNumber));
        }

        return new PricingRuleSetVersion
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            RuleSetId = ruleSetId,
            VersionNumber = versionNumber,
            Status = PricingVersionStatus.Draft,
            Definition = definition,
            CreatedAt = EnsureUtc(nowUtc),
            CreatedBy = createdBy,
        };
    }

    /// <summary>更新草稿定义（仅 Draft 可编辑；Published/Superseded 不可原地修改，AGENTS §6.4）。</summary>
    /// <exception cref="PricingRuleViolationException">非 Draft 状态或定义校验失败时抛出。</exception>
    public void UpdateDraftDefinition(PricingRuleDefinition definition, DateTime nowUtc, Guid? updatedBy = null)
    {
        if (Status != PricingVersionStatus.Draft)
        {
            throw new PricingRuleViolationException(
                $"版本 {VersionNumber} 状态为 {Status}，已发布版本不可原地修改；修改请创建新版本。");
        }

        var errors = PricingDefinitionValidator.Validate(definition);
        if (errors.Count > 0)
        {
            throw new PricingRuleViolationException(
                $"规则定义校验未通过（{errors.Count} 项）：{string.Join("；", errors)}");
        }

        Definition = definition;
        Touch(nowUtc, updatedBy);
    }

    /// <summary>发布草稿（调用方保证规则集启用且无在发版本；本方法守护状态迁移）。</summary>
    /// <exception cref="PricingRuleViolationException">非 Draft 状态时抛出。</exception>
    public void Publish(DateTime nowUtc, Guid? publishedBy)
    {
        if (Status != PricingVersionStatus.Draft)
        {
            throw new PricingRuleViolationException($"版本 {VersionNumber} 状态为 {Status}，仅草稿可发布。");
        }

        Status = PricingVersionStatus.Published;
        PublishedAt = EnsureUtc(nowUtc);
        PublishedBy = publishedBy;
        Touch(nowUtc, publishedBy);
    }

    /// <summary>标记被新版本取代（仅 Published 可被取代；历史保留）。</summary>
    /// <exception cref="PricingRuleViolationException">非 Published 状态时抛出。</exception>
    public void MarkSuperseded(DateTime nowUtc, Guid? updatedBy)
    {
        if (Status != PricingVersionStatus.Published)
        {
            throw new PricingRuleViolationException($"版本 {VersionNumber} 状态为 {Status}，仅已发布版本可被取代。");
        }

        Status = PricingVersionStatus.Superseded;
        Touch(nowUtc, updatedBy);
    }
}
