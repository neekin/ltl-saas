using Ltl.Modules.Pricing.Domain;

namespace Ltl.Modules.Pricing.Application;

/// <summary>
/// 计价规则集与版本生命周期服务（PRICE-001）。租户 Id 由 DbContext 提供（无上下文 → fail-closed）。
/// 无 HTTP 端点（与 ORG-001/CUS-001 边界一致：认证交付前不暴露业务 API）。
/// </summary>
public interface IPricingRuleSetService
{
    /// <summary>创建规则集（启用状态）。</summary>
    Task<PricingRuleSet> CreateRuleSetAsync(string code, string name, DateTime nowUtc, Guid? createdBy = null);

    /// <summary>重命名规则集。</summary>
    Task RenameRuleSetAsync(Guid ruleSetId, string name, DateTime nowUtc, Guid? updatedBy = null);

    /// <summary>停用规则集（幂等）：停止全部版本匹配，历史保留。</summary>
    Task DisableRuleSetAsync(Guid ruleSetId, DateTime nowUtc, Guid? updatedBy = null);

    /// <summary>启用规则集（幂等）。</summary>
    Task EnableRuleSetAsync(Guid ruleSetId, DateTime nowUtc, Guid? updatedBy = null);

    /// <summary>创建草稿版本（定义先经语义校验；每集合同时最多一个草稿；版本号集合内递增）。</summary>
    Task<PricingRuleSetVersion> CreateDraftVersionAsync(
        Guid ruleSetId,
        PricingRuleDefinition definition,
        DateTime nowUtc,
        Guid? createdBy = null);

    /// <summary>更新草稿定义（仅 Draft；已发布版本不可原地修改）。</summary>
    Task UpdateDraftDefinitionAsync(
        Guid versionId,
        PricingRuleDefinition definition,
        DateTime nowUtc,
        Guid? updatedBy = null);

    /// <summary>试校验版本当前定义，返回全部语义错误（零错误 = 空列表；不改状态）。</summary>
    Task<IReadOnlyList<string>> ValidateVersionAsync(Guid versionId);

    /// <summary>
    /// 发布版本（事务内：重校验 → Draft→Published → 旧 Published→Superseded）。
    /// 要求规则集启用；发布后同集合仅一个 Published。
    /// </summary>
    Task PublishVersionAsync(Guid versionId, DateTime nowUtc, Guid? publishedBy = null);

    /// <summary>规则集列表（租户内，按编码排序）。</summary>
    Task<IReadOnlyList<PricingRuleSet>> GetRuleSetsAsync();

    /// <summary>指定规则集的版本历史（版本号降序）。</summary>
    Task<IReadOnlyList<PricingRuleSetVersion>> GetVersionsAsync(Guid ruleSetId);

    /// <summary>当前生效（Published）版本；无则返回 null（PRICE-002 执行器入口）。</summary>
    Task<PricingRuleSetVersion?> GetCurrentPublishedVersionAsync(Guid ruleSetId);
}
