using Ltl.Modules.Pricing.Application;
using Ltl.Modules.Pricing.Domain;
using Ltl.Modules.Pricing.Infrastructure;
using Ltl.Modules.Tenancy.Domain;
using Ltl.SharedKernel.Application;
using Xunit;

namespace Ltl.IntegrationTests.Pricing;

/// <summary>
/// PRICE-002 集成测试：对已发布版本试算、停用/无版本/跨租户报错、
/// 版本取代后历史快照结果不变（规则变更不影响历史，AGENTS §6.4）。
/// </summary>
public sealed class PricingTrialTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    private Guid _tenantId;

    private async Task SeedAsync()
    {
        var tenant = Tenant.Create($"trial-{Guid.NewGuid():N}", "试算测试租户", DateTime.UtcNow);
        await using (var tenancy = fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenant);
            await tenancy.SaveChangesAsync();
        }

        _tenantId = tenant.Id;
    }

    private PricingRuleSetService CreateRuleSetService(Guid? tenantId = null) =>
        new(fixture.CreatePricingContext(tenantId ?? _tenantId));

    private PricingTrialService CreateTrialService(Guid? tenantId = null) =>
        new(fixture.CreatePricingContext(tenantId ?? _tenantId));

    private static PricingRuleDefinition Definition(string ruleName, decimal unitPrice) => new()
    {
        Rounding = RoundingPolicy.HalfUpTwoDecimals,
        Rules =
        [
            new PricingRule
            {
                Priority = 1,
                Name = ruleName,
                Enabled = true,
                BillingWeightStrategy = BillingWeightStrategy.WeightOnly,
                Conditions = [],
                Freight = new PricingFreightFeeItem
                {
                    Name = "运费",
                    Type = PricingFreightType.ByWeight,
                    UnitPrice = unitPrice,
                },
                Surcharges = [],
            },
        ],
    };

    private static PricingQuoteInput Input(decimal weight) => new()
    {
        WeightKg = weight,
        VolumeM3 = 0m,
        PieceCount = 1,
    };

    [Fact]
    public async Task TrialCalculate_AgainstPublishedVersion_ShouldReturnVersionedQuote()
    {
        await SeedAsync();
        var ruleSets = CreateRuleSetService();
        var ruleSet = await ruleSets.CreateRuleSetAsync("std", "标准价卡", DateTime.UtcNow);
        var version = await ruleSets.CreateDraftVersionAsync(
            ruleSet.Id, Definition("v1-运价", 1.5m), DateTime.UtcNow);
        await ruleSets.PublishVersionAsync(version.Id, DateTime.UtcNow);

        var result = await CreateTrialService().TrialCalculateAsync(ruleSet.Id, Input(weight: 10m));

        Assert.Equal(ruleSet.Id, result.RuleSetId);
        Assert.Equal("std", result.RuleSetCode);
        Assert.Equal(version.Id, result.VersionId);
        Assert.Equal(1, result.VersionNumber);
        Assert.Equal("v1-运价", result.Quote.MatchedRuleName);
        Assert.Equal(15m, result.Quote.Total);
        Assert.Equal(10m, result.Input.WeightKg); // 输入快照回显
    }

    [Fact]
    public async Task TrialCalculate_DisabledRuleSet_ShouldBeRejected()
    {
        await SeedAsync();
        var ruleSets = CreateRuleSetService();
        var ruleSet = await ruleSets.CreateRuleSetAsync("std", "标准价卡", DateTime.UtcNow);
        var version = await ruleSets.CreateDraftVersionAsync(ruleSet.Id, Definition("运价", 1.5m), DateTime.UtcNow);
        await ruleSets.PublishVersionAsync(version.Id, DateTime.UtcNow);
        await ruleSets.DisableRuleSetAsync(ruleSet.Id, DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<PricingRuleViolationException>(
            () => CreateTrialService().TrialCalculateAsync(ruleSet.Id, Input(weight: 10m)));
        Assert.Contains("已停用", ex.Message);
    }

    [Fact]
    public async Task TrialCalculate_WithoutPublishedVersion_ShouldBeRejected()
    {
        await SeedAsync();
        var ruleSet = await CreateRuleSetService().CreateRuleSetAsync("draft-only", "仅草稿价卡", DateTime.UtcNow);
        await CreateRuleSetService().CreateDraftVersionAsync(ruleSet.Id, Definition("运价", 1.5m), DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<PricingRuleViolationException>(
            () => CreateTrialService().TrialCalculateAsync(ruleSet.Id, Input(weight: 10m)));
        Assert.Contains("没有已发布", ex.Message);
    }

    [Fact]
    public async Task TrialCalculate_CrossTenantRuleSet_ShouldBeEntityNotFound()
    {
        await SeedAsync();
        var otherTenant = Tenant.Create($"trial-other-{Guid.NewGuid():N}", "其他租户", DateTime.UtcNow);
        await using (var tenancy = fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(otherTenant);
            await tenancy.SaveChangesAsync();
        }

        var ruleSet = await CreateRuleSetService().CreateRuleSetAsync("std", "标准价卡", DateTime.UtcNow);
        var version = await CreateRuleSetService().CreateDraftVersionAsync(
            ruleSet.Id, Definition("运价", 1.5m), DateTime.UtcNow);
        await CreateRuleSetService().PublishVersionAsync(version.Id, DateTime.UtcNow);

        // 其他租户按 Id 访问 → 过滤器不可见 → EntityNotFound（404 语义）
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => CreateTrialService(otherTenant.Id).TrialCalculateAsync(ruleSet.Id, Input(weight: 10m)));
    }

    [Fact]
    public async Task SupersededVersion_ShouldKeepHistoricalDefinitionAndResult()
    {
        await SeedAsync();
        var ruleSets = CreateRuleSetService();
        var ruleSet = await ruleSets.CreateRuleSetAsync("std", "标准价卡", DateTime.UtcNow);

        var v1 = await ruleSets.CreateDraftVersionAsync(ruleSet.Id, Definition("v1-运价", 1.5m), DateTime.UtcNow);
        await ruleSets.PublishVersionAsync(v1.Id, DateTime.UtcNow);

        var v2 = await ruleSets.CreateDraftVersionAsync(ruleSet.Id, Definition("v2-运价", 2m), DateTime.UtcNow);
        await ruleSets.PublishVersionAsync(v2.Id, DateTime.UtcNow);

        // 当前生效版本 → 新价
        var current = await CreateTrialService().TrialCalculateAsync(ruleSet.Id, Input(weight: 10m));
        Assert.Equal(2, current.VersionNumber);
        Assert.Equal(20m, current.Quote.Total);

        // 旧版本快照保留：按 v1 定义计算结果不变（规则变更不影响历史，可追溯）
        var versions = await ruleSets.GetVersionsAsync(ruleSet.Id);
        var historical = versions.Single(v => v.VersionNumber == 1);
        Assert.Equal(PricingVersionStatus.Superseded, historical.Status);
        Assert.Equal("v1-运价", historical.Definition.Rules[0].Name);

        var historicalQuote = PricingEngine.Calculate(historical.Definition, Input(weight: 10m));
        Assert.Equal(15m, historicalQuote.Total);
    }
}
