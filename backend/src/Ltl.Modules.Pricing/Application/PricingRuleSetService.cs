using Ltl.Modules.Pricing.Domain;
using Ltl.Modules.Pricing.Infrastructure;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Pricing.Application;

/// <summary>
/// 规则集与版本生命周期实现。所有实体查询经租户过滤器（跨租户 Id → 不可见 → EntityNotFoundException）；
/// 租户 Id 取自 DbContext（无上下文 → fail-closed）。
/// </summary>
public sealed class PricingRuleSetService(PricingDbContext db)
    : IPricingRuleSetService
{
    public async Task<PricingRuleSet> CreateRuleSetAsync(
        string code, string name, DateTime nowUtc, Guid? createdBy = null)
    {
        var tenantId = RequireTenantId();

        var ruleSet = PricingRuleSet.Create(tenantId, code, name, nowUtc, createdBy);
        db.RuleSets.Add(ruleSet);
        await db.SaveChangesAsync();
        return ruleSet;
    }

    public async Task RenameRuleSetAsync(Guid ruleSetId, string name, DateTime nowUtc, Guid? updatedBy = null)
    {
        var ruleSet = await RequireRuleSetAsync(ruleSetId);
        ruleSet.Rename(name, nowUtc, updatedBy);
        await db.SaveChangesAsync();
    }

    public async Task DisableRuleSetAsync(Guid ruleSetId, DateTime nowUtc, Guid? updatedBy = null)
    {
        var ruleSet = await RequireRuleSetAsync(ruleSetId);
        ruleSet.Disable(nowUtc, updatedBy);
        await db.SaveChangesAsync();
    }

    public async Task EnableRuleSetAsync(Guid ruleSetId, DateTime nowUtc, Guid? updatedBy = null)
    {
        var ruleSet = await RequireRuleSetAsync(ruleSetId);
        ruleSet.Enable(nowUtc, updatedBy);
        await db.SaveChangesAsync();
    }

    public async Task<PricingRuleSetVersion> CreateDraftVersionAsync(
        Guid ruleSetId,
        PricingRuleDefinition definition,
        DateTime nowUtc,
        Guid? createdBy = null)
    {
        var ruleSet = await RequireRuleSetAsync(ruleSetId);

        var hasDraft = await db.RuleSetVersions
            .AnyAsync(v => v.RuleSetId == ruleSetId && v.Status == PricingVersionStatus.Draft);
        if (hasDraft)
        {
            throw new PricingRuleViolationException(
                $"规则集 {ruleSet.Code} 已存在草稿版本；请先处理现有草稿（修改或作废）再创建新草稿。");
        }

        // 版本号 = 集合内递增（内部元数据，非业务单号；(tenant_id, rule_set_id, version_number)
        // 唯一索引兜底，并发冲突显式报错可重试——PRICE-001 设计文档第 3 节）
        var nextVersion = await db.RuleSetVersions
            .Where(v => v.RuleSetId == ruleSetId)
            .MaxAsync(v => (int?)v.VersionNumber) ?? 0;

        var version = PricingRuleSetVersion.CreateDraft(
            ruleSet.TenantId, ruleSetId, nextVersion + 1, definition, nowUtc, createdBy);
        db.RuleSetVersions.Add(version);
        await db.SaveChangesAsync();
        return version;
    }

    public async Task UpdateDraftDefinitionAsync(
        Guid versionId,
        PricingRuleDefinition definition,
        DateTime nowUtc,
        Guid? updatedBy = null)
    {
        var version = await RequireVersionAsync(versionId);
        version.UpdateDraftDefinition(definition, nowUtc, updatedBy);
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<string>> ValidateVersionAsync(Guid versionId)
    {
        var version = await RequireVersionAsync(versionId);
        return PricingDefinitionValidator.Validate(version.Definition);
    }

    public async Task PublishVersionAsync(Guid versionId, DateTime nowUtc, Guid? publishedBy = null)
    {
        var version = await RequireVersionAsync(versionId);
        var ruleSet = await RequireRuleSetAsync(version.RuleSetId);

        if (ruleSet.Status != PricingRuleSetStatus.Active)
        {
            throw new PricingRuleViolationException($"规则集 {ruleSet.Code} 已停用，不能发布版本。");
        }

        // 发布前重校验（写入口已校验，此处防御外部篡改/历史脏数据）
        var errors = PricingDefinitionValidator.Validate(version.Definition);
        if (errors.Count > 0)
        {
            throw new PricingRuleViolationException(
                $"规则定义校验未通过（{errors.Count} 项）：{string.Join("；", errors)}");
        }

        var currentPublished = await db.RuleSetVersions
            .SingleOrDefaultAsync(v =>
                v.RuleSetId == version.RuleSetId && v.Status == PricingVersionStatus.Published);

        if (currentPublished is null)
        {
            version.Publish(nowUtc, publishedBy);
            await db.SaveChangesAsync();
            return;
        }

        // PostgreSQL 部分唯一索引按语句即时校验（非事务级延迟），同批 SaveChanges 语句顺序不受控：
        // 必须先提交"旧版本 → Superseded"、再提交"新版本 → Published"，两个语句间不存在两行 Published。
        // 同一显式事务内执行保证原子性（外部观察者永远看不到中间状态）。
        await using var transaction = await db.Database.BeginTransactionAsync();
        currentPublished.MarkSuperseded(nowUtc, publishedBy);
        await db.SaveChangesAsync();
        version.Publish(nowUtc, publishedBy);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<IReadOnlyList<PricingRuleSet>> GetRuleSetsAsync()
    {
        return await db.RuleSets
            .OrderBy(s => s.Code)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<PricingRuleSetVersion>> GetVersionsAsync(Guid ruleSetId)
    {
        await RequireRuleSetAsync(ruleSetId);

        return await db.RuleSetVersions
            .Where(v => v.RuleSetId == ruleSetId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync();
    }

    public async Task<PricingRuleSetVersion?> GetCurrentPublishedVersionAsync(Guid ruleSetId)
    {
        await RequireRuleSetAsync(ruleSetId);

        return await db.RuleSetVersions
            .SingleOrDefaultAsync(v =>
                v.RuleSetId == ruleSetId && v.Status == PricingVersionStatus.Published);
    }

    private Guid RequireTenantId()
    {
        if (db.CurrentTenantId is not { } tenantId)
        {
            throw new TenantContextMissingException();
        }

        return tenantId;
    }

    private async Task<PricingRuleSet> RequireRuleSetAsync(Guid ruleSetId)
    {
        return await db.RuleSets.FindAsync([ruleSetId])
            ?? throw new EntityNotFoundException("计价规则集", ruleSetId);
    }

    private async Task<PricingRuleSetVersion> RequireVersionAsync(Guid versionId)
    {
        return await db.RuleSetVersions.FindAsync([versionId])
            ?? throw new EntityNotFoundException("计价规则版本", versionId);
    }
}
