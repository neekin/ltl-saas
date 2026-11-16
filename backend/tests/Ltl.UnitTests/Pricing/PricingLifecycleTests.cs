using Ltl.Modules.Pricing.Domain;
using Xunit;

namespace Ltl.UnitTests.Pricing;

/// <summary>PRICE-001 版本生命周期与规则集实体领域守卫测试。</summary>
public class PricingLifecycleTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private static PricingRuleDefinition ValidDefinition() => new()
    {
        Rounding = RoundingPolicy.HalfUpTwoDecimals,
        Rules =
        [
            new PricingRule
            {
                Priority = 1,
                Name = "标准运价",
                Enabled = true,
                BillingWeightStrategy = BillingWeightStrategy.WeightOnly,
                Conditions = [],
                Freight = new PricingFreightFeeItem
                {
                    Name = "运费",
                    Type = PricingFreightType.ByWeight,
                    UnitPrice = 1.5m,
                },
                Surcharges = [],
            },
        ],
    };

    private static PricingRuleDefinition InvalidDefinition() => new()
    {
        Rounding = RoundingPolicy.HalfUpTwoDecimals,
        Rules = [],
    };

    [Fact]
    public void CreateRuleSet_ShouldStartActive()
    {
        var ruleSet = PricingRuleSet.Create(TenantId, "gz-wh-std", "广州—武汉标准价卡", Now);

        Assert.Equal(PricingRuleSetStatus.Active, ruleSet.Status);
        Assert.Equal("gz-wh-std", ruleSet.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void CreateRuleSet_WithBlankCode_ShouldThrow(string? code)
    {
        Assert.Throws<ArgumentException>(
            () => PricingRuleSet.Create(TenantId, code!, "名称", Now));
    }

    [Fact]
    public void CreateDraft_WithInvalidDefinition_ShouldThrowWithAllErrors()
    {
        var ex = Assert.Throws<PricingRuleViolationException>(
            () => PricingRuleSetVersion.CreateDraft(TenantId, Guid.NewGuid(), 1, InvalidDefinition(), Now));

        Assert.Contains("校验未通过", ex.Message);
    }

    [Fact]
    public void CreateDraft_WithZeroVersionNumber_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            () => PricingRuleSetVersion.CreateDraft(TenantId, Guid.NewGuid(), 0, ValidDefinition(), Now));
    }

    [Fact]
    public void DraftLifecycle_PublishThenImmutable()
    {
        var version = PricingRuleSetVersion.CreateDraft(
            TenantId, Guid.NewGuid(), 1, ValidDefinition(), Now, createdBy: Guid.NewGuid());

        Assert.Equal(PricingVersionStatus.Draft, version.Status);
        Assert.Null(version.PublishedAt);

        var publishedAt = DateTime.UtcNow;
        version.Publish(publishedAt, Guid.NewGuid());

        Assert.Equal(PricingVersionStatus.Published, version.Status);
        Assert.NotNull(version.PublishedAt);

        // 已发布版本不可原地修改（AGENTS §6.4）
        var ex = Assert.Throws<PricingRuleViolationException>(
            () => version.UpdateDraftDefinition(ValidDefinition(), DateTime.UtcNow));
        Assert.Contains("不可原地修改", ex.Message);

        // 再次发布拒绝
        Assert.Throws<PricingRuleViolationException>(() => version.Publish(DateTime.UtcNow, null));
    }

    [Fact]
    public void Publish_ThenSupersede_ThenGuardsHold()
    {
        var version = PricingRuleSetVersion.CreateDraft(TenantId, Guid.NewGuid(), 1, ValidDefinition(), Now);
        version.Publish(DateTime.UtcNow, Guid.NewGuid());

        version.MarkSuperseded(DateTime.UtcNow, Guid.NewGuid());
        Assert.Equal(PricingVersionStatus.Superseded, version.Status);

        Assert.Throws<PricingRuleViolationException>(
            () => version.UpdateDraftDefinition(ValidDefinition(), DateTime.UtcNow));
        Assert.Throws<PricingRuleViolationException>(
            () => version.MarkSuperseded(DateTime.UtcNow, Guid.NewGuid()));
    }

    [Fact]
    public void DraftDefinition_ShouldBeReplaceable()
    {
        var version = PricingRuleSetVersion.CreateDraft(TenantId, Guid.NewGuid(), 1, ValidDefinition(), Now);

        var updated = new PricingRuleDefinition
        {
            Rounding = RoundingPolicy.HalfUpTwoDecimals,
            Rules =
            [
                new PricingRule
                {
                    Priority = 1,
                    Name = "修改后的规则",
                    Enabled = true,
                    BillingWeightStrategy = BillingWeightStrategy.VolumeOnly,
                    Conditions = [],
                    Freight = new PricingFreightFeeItem { Name = "运费", Type = PricingFreightType.ByVolume, UnitPrice = 80m },
                    Surcharges = [],
                },
            ],
        };

        version.UpdateDraftDefinition(updated, DateTime.UtcNow, Guid.NewGuid());
        Assert.Equal("修改后的规则", version.Definition.Rules[0].Name);
    }

    [Fact]
    public void RuleSetDisableEnable_ShouldBeIdempotent()
    {
        var ruleSet = PricingRuleSet.Create(TenantId, "std", "标准价卡", Now);

        ruleSet.Disable(Now);
        ruleSet.Disable(Now);
        Assert.Equal(PricingRuleSetStatus.Disabled, ruleSet.Status);

        ruleSet.Enable(Now);
        ruleSet.Enable(Now);
        Assert.Equal(PricingRuleSetStatus.Active, ruleSet.Status);
    }

    [Fact]
    public void RenameRuleSet_WithBlankName_ShouldThrow()
    {
        var ruleSet = PricingRuleSet.Create(TenantId, "std", "标准价卡", Now);
        Assert.Throws<ArgumentException>(() => ruleSet.Rename(" ", Now));
    }
}
