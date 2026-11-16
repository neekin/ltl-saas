using Ltl.Modules.Pricing.Domain;
using Ltl.Modules.Pricing.Infrastructure;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Pricing.Application;

/// <summary>
/// 试算实现：按租户过滤器加载规则集与当前发布版本（跨租户规则集 → 不可见 → 404 语义），
/// 执行器为纯函数（版本定义快照 + 输入 → 结果），历史版本快照不随新发布变化。
/// </summary>
public sealed class PricingTrialService(PricingDbContext db) : IPricingTrialService
{
    public async Task<PricingTrialResult> TrialCalculateAsync(Guid ruleSetId, PricingQuoteInput input)
    {
        var ruleSet = await db.RuleSets.FindAsync([ruleSetId])
            ?? throw new EntityNotFoundException("计价规则集", ruleSetId);

        if (ruleSet.Status != PricingRuleSetStatus.Active)
        {
            throw new PricingRuleViolationException($"规则集 {ruleSet.Code} 已停用，不能执行计价。");
        }

        var version = await db.RuleSetVersions
            .SingleOrDefaultAsync(v =>
                v.RuleSetId == ruleSetId && v.Status == PricingVersionStatus.Published)
            ?? throw new PricingRuleViolationException(
                $"规则集 {ruleSet.Code} 没有已发布的生效版本，不能执行计价。");

        var quote = PricingEngine.Calculate(version.Definition, input);

        return new PricingTrialResult
        {
            RuleSetId = ruleSet.Id,
            RuleSetCode = ruleSet.Code,
            VersionId = version.Id,
            VersionNumber = version.VersionNumber,
            Input = input,
            Quote = quote,
        };
    }
}
