using Ltl.Modules.Pricing.Application;
using Ltl.Modules.Pricing.Domain;
using Ltl.Modules.Pricing.Infrastructure;
using Ltl.Modules.Tenancy.Domain;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ltl.IntegrationTests.Pricing;

/// <summary>
/// PRICE-001 集成测试：迁移、租户范围唯一、部分唯一索引（单一 Published/Draft）、
/// 跨租户复合外键、过滤器隔离 + fail-closed、发布流、已发布不可变、JSON 精度往返、xmin 并发。
/// </summary>
public sealed class PricingPersistenceTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    private Guid _tenantId;

    private async Task SeedAsync()
    {
        var tenant = Tenant.Create($"pricing-{Guid.NewGuid():N}", "计价测试租户", DateTime.UtcNow);
        await using (var tenancy = fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenant);
            await tenancy.SaveChangesAsync();
        }

        _tenantId = tenant.Id;
    }

    private PricingRuleSetService CreateService(Guid? tenantId = null) =>
        new(fixture.CreatePricingContext(tenantId ?? _tenantId));

    private static PricingRuleDefinition ValidDefinition(
        string ruleName = "标准运价",
        decimal unitPrice = 1.5m,
        BillingWeightStrategy strategy = BillingWeightStrategy.WeightOnly,
        decimal? factor = null) => new()
        {
            Rounding = RoundingPolicy.HalfUpTwoDecimals,
            Rules =
            [
                new PricingRule
                {
                    Priority = 1,
                    Name = ruleName,
                    Enabled = true,
                    BillingWeightStrategy = strategy,
                    VolumeToWeightFactor = factor,
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

    [Fact]
    public async Task Migration_ShouldApplyOnEmptyDatabase()
    {
        await using var context = fixture.CreatePricingContext();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(0, await context.RuleSets.CountAsync());
    }

    [Fact]
    public async Task SameTenantDuplicateCode_ShouldBeRejected()
    {
        await SeedAsync();
        await using var context = fixture.CreatePricingContext(_tenantId);

        context.RuleSets.Add(PricingRuleSet.Create(_tenantId, "std", "标准价卡", DateTime.UtcNow));
        await context.SaveChangesAsync();

        context.RuleSets.Add(PricingRuleSet.Create(_tenantId, "std", "重复编码", DateTime.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task SameCodeAcrossTenants_ShouldBeAllowed()
    {
        await SeedAsync();
        var otherTenant = Tenant.Create($"pricing-other-{Guid.NewGuid():N}", "其他租户", DateTime.UtcNow);
        await using (var tenancy = fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(otherTenant);
            await tenancy.SaveChangesAsync();
        }

        var first = await CreateService().CreateRuleSetAsync("std", "标准价卡", DateTime.UtcNow);
        var second = await CreateService(otherTenant.Id).CreateRuleSetAsync("std", "其他租户标准价卡", DateTime.UtcNow);

        Assert.Equal("std", first.Code);
        Assert.Equal("std", second.Code);
        Assert.NotEqual(first.TenantId, second.TenantId);
    }

    [Fact]
    public async Task VersionReferencingOtherTenantRuleSet_ShouldBeRejectedByCompositeForeignKey()
    {
        await SeedAsync();
        var tenantB = Tenant.Create($"pricing-b-{Guid.NewGuid():N}", "租户乙", DateTime.UtcNow);
        await using (var tenancy = fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenantB);
            await tenancy.SaveChangesAsync();
        }

        var ruleSetA = await CreateService().CreateRuleSetAsync("set-a", "租户甲价卡", DateTime.UtcNow);

        // 租户乙的版本行引用租户甲的规则集 → 复合外键拒绝
        var versionB = PricingRuleSetVersion.CreateDraft(
            tenantB.Id, ruleSetA.Id, 1, ValidDefinition(), DateTime.UtcNow);
        await using var contextB = fixture.CreatePricingContext(tenantB.Id);
        contextB.RuleSetVersions.Add(versionB);

        await Assert.ThrowsAsync<DbUpdateException>(() => contextB.SaveChangesAsync());
    }

    [Fact]
    public async Task SecondPublishedVersion_ShouldBeRejectedByPartialUniqueIndex()
    {
        await SeedAsync();
        var ruleSet = await CreateService().CreateRuleSetAsync("std", "标准价卡", DateTime.UtcNow);

        var v1 = await CreateService().CreateDraftVersionAsync(ruleSet.Id, ValidDefinition(), DateTime.UtcNow);
        await CreateService().PublishVersionAsync(v1.Id, DateTime.UtcNow);

        // 绕过服务直接插入第二个 Published（状态经 Publish 迁移后添加）
        var v2 = await CreateService().CreateDraftVersionAsync(ruleSet.Id, ValidDefinition(), DateTime.UtcNow);
        v2.Publish(DateTime.UtcNow, null);
        await using var context = fixture.CreatePricingContext(_tenantId);
        context.RuleSetVersions.Add(v2);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task SecondDraft_ShouldBeRejectedByPartialUniqueIndex()
    {
        await SeedAsync();
        var ruleSet = await CreateService().CreateRuleSetAsync("std", "标准价卡", DateTime.UtcNow);
        await CreateService().CreateDraftVersionAsync(ruleSet.Id, ValidDefinition(), DateTime.UtcNow);

        // 绕过服务直接插入第二个草稿
        var secondDraft = PricingRuleSetVersion.CreateDraft(
            _tenantId, ruleSet.Id, 99, ValidDefinition(), DateTime.UtcNow);
        await using var context = fixture.CreatePricingContext(_tenantId);
        context.RuleSetVersions.Add(secondDraft);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task CreateDraftVersion_WhenDraftExists_ShouldBeRejectedByService()
    {
        await SeedAsync();
        var ruleSet = await CreateService().CreateRuleSetAsync("std", "标准价卡", DateTime.UtcNow);
        await CreateService().CreateDraftVersionAsync(ruleSet.Id, ValidDefinition(), DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<PricingRuleViolationException>(
            () => CreateService().CreateDraftVersionAsync(ruleSet.Id, ValidDefinition(), DateTime.UtcNow));

        Assert.Contains("已存在草稿", ex.Message);
    }

    [Fact]
    public async Task PublishFlow_ShouldSupersedePreviousPublishedAndExposeCurrent()
    {
        await SeedAsync();
        var service = CreateService();
        var ruleSet = await service.CreateRuleSetAsync("std", "标准价卡", DateTime.UtcNow);

        var v1 = await service.CreateDraftVersionAsync(ruleSet.Id, ValidDefinition(ruleName: "v1-运价"), DateTime.UtcNow);
        await service.PublishVersionAsync(v1.Id, DateTime.UtcNow);

        var v2 = await service.CreateDraftVersionAsync(ruleSet.Id, ValidDefinition(ruleName: "v2-运价"), DateTime.UtcNow);
        await service.PublishVersionAsync(v2.Id, DateTime.UtcNow);

        var versions = await service.GetVersionsAsync(ruleSet.Id);
        Assert.Equal(2, versions.Count);
        Assert.All(versions, v => Assert.NotEqual(PricingVersionStatus.Draft, v.Status));
        Assert.Contains(versions, v => v.VersionNumber == 1 && v.Status == PricingVersionStatus.Superseded);
        Assert.Contains(versions, v => v.VersionNumber == 2 && v.Status == PricingVersionStatus.Published);

        var current = await service.GetCurrentPublishedVersionAsync(ruleSet.Id);
        Assert.NotNull(current);
        Assert.Equal(2, current.VersionNumber);
        Assert.Equal("v2-运价", current.Definition.Rules[0].Name);
    }

    [Fact]
    public async Task PublishToDisabledRuleSet_ShouldBeRejected()
    {
        await SeedAsync();
        var service = CreateService();
        var ruleSet = await service.CreateRuleSetAsync("std", "标准价卡", DateTime.UtcNow);
        var version = await service.CreateDraftVersionAsync(ruleSet.Id, ValidDefinition(), DateTime.UtcNow);

        await service.DisableRuleSetAsync(ruleSet.Id, DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<PricingRuleViolationException>(
            () => service.PublishVersionAsync(version.Id, DateTime.UtcNow));
        Assert.Contains("已停用", ex.Message);
    }

    [Fact]
    public async Task UpdateDraftDefinition_OnPublishedVersion_ShouldBeRejectedAndSnapshotUnchanged()
    {
        await SeedAsync();
        var service = CreateService();
        var ruleSet = await service.CreateRuleSetAsync("std", "标准价卡", DateTime.UtcNow);
        var version = await service.CreateDraftVersionAsync(ruleSet.Id, ValidDefinition(ruleName: "原始"), DateTime.UtcNow);
        await service.PublishVersionAsync(version.Id, DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<PricingRuleViolationException>(
            () => service.UpdateDraftDefinitionAsync(
                version.Id, ValidDefinition(ruleName: "篡改"), DateTime.UtcNow));
        Assert.Contains("不可原地修改", ex.Message);

        // 持久层验证：快照未被改动
        await using var verification = fixture.CreatePricingContext(_tenantId);
        var reloaded = await verification.RuleSetVersions.SingleAsync(v => v.Id == version.Id);
        Assert.Equal("原始", reloaded.Definition.Rules[0].Name);
    }

    [Fact]
    public async Task Definition_ShouldRoundTripWithDecimalPrecision()
    {
        await SeedAsync();
        var service = CreateService();
        var ruleSet = await service.CreateRuleSetAsync("tiered", "阶梯价卡", DateTime.UtcNow);

        var definition = new PricingRuleDefinition
        {
            Rounding = RoundingPolicy.HalfUpTwoDecimals,
            Rules =
            [
                new PricingRule
                {
                    Priority = 1,
                    Name = "重货阶梯",
                    Enabled = true,
                    BillingWeightStrategy = BillingWeightStrategy.WeightVolumeMax,
                    VolumeToWeightFactor = 333.33m,
                    Conditions = [],
                    Freight = new PricingFreightFeeItem
                    {
                        Name = "运费",
                        Type = PricingFreightType.Tiered,
                        MinCharge = 35.5m,
                        Tiers =
                        [
                            new PricingTier { UpTo = 3.5m, UnitPrice = 0.35m },
                            new PricingTier { UpTo = null, UnitPrice = 1.25m },
                        ],
                    },
                    Surcharges = [],
                },
            ],
        };

        var version = await service.CreateDraftVersionAsync(ruleSet.Id, definition, DateTime.UtcNow);

        await using var verification = fixture.CreatePricingContext(_tenantId);
        var reloaded = await verification.RuleSetVersions.SingleAsync(v => v.Id == version.Id);
        var rule = Assert.Single(reloaded.Definition.Rules);
        var tiers = rule.Freight.Tiers!;

        Assert.Equal(BillingWeightStrategy.WeightVolumeMax, rule.BillingWeightStrategy);
        Assert.Equal(333.33m, rule.VolumeToWeightFactor);
        Assert.Equal(35.5m, rule.Freight.MinCharge);
        Assert.Equal(3.5m, tiers[0].UpTo);
        Assert.Equal(0.35m, tiers[0].UnitPrice);
        Assert.Null(tiers[1].UpTo);
        Assert.Equal(1.25m, tiers[1].UnitPrice);
    }

    [Fact]
    public async Task TenantIsolation_ShouldFilterRuleSetsAndFailClosed()
    {
        await SeedAsync();
        var tenantB = Tenant.Create($"pricing-iso-{Guid.NewGuid():N}", "隔离租户", DateTime.UtcNow);
        await using (var tenancy = fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenantB);
            await tenancy.SaveChangesAsync();
        }

        var setA = await CreateService().CreateRuleSetAsync("set-a", "租户甲价卡", DateTime.UtcNow);
        await CreateService(tenantB.Id).CreateRuleSetAsync("set-b", "租户乙价卡", DateTime.UtcNow);

        // 租户甲只见自己的规则集
        await using var contextA = fixture.CreatePricingContext(_tenantId);
        Assert.Single(await contextA.RuleSets.ToListAsync());
        Assert.Equal(setA.Id, (await contextA.RuleSets.SingleAsync()).Id);

        // 无租户上下文 → fail-closed（0 行）
        await using var contextNone = fixture.CreatePricingContext();
        Assert.Empty(await contextNone.RuleSets.ToListAsync());

        // 跨租户 Id 查询 → 视为不存在
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => CreateService(tenantB.Id).GetVersionsAsync(setA.Id));
    }

    [Fact]
    public async Task ConcurrentRename_ShouldTriggerXminConcurrency()
    {
        await SeedAsync();
        var ruleSet = await CreateService().CreateRuleSetAsync("std", "标准价卡", DateTime.UtcNow);

        await using var context1 = fixture.CreatePricingContext(_tenantId);
        await using var context2 = fixture.CreatePricingContext(_tenantId);

        var loaded1 = await context1.RuleSets.SingleAsync(s => s.Id == ruleSet.Id);
        var loaded2 = await context2.RuleSets.SingleAsync(s => s.Id == ruleSet.Id);

        loaded1.Rename("第一次改名", DateTime.UtcNow);
        await context1.SaveChangesAsync();

        loaded2.Rename("第二次改名", DateTime.UtcNow);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context2.SaveChangesAsync());
    }
}
