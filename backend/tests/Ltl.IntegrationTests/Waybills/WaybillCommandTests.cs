using Ltl.Modules.Customers.Domain;
using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Pricing.Application;
using Ltl.Modules.Pricing.Domain;
using Ltl.Modules.Platform.Application;
using Ltl.Modules.Platform.Domain;
using Ltl.Modules.Tenancy.Domain;
using Ltl.Modules.Waybills.Application;
using Ltl.Modules.Waybills.Domain;
using Ltl.Modules.Waybills.Infrastructure;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ltl.IntegrationTests.Waybills;

/// <summary>
/// WB-002 集成测试：开单全流程（计价固化 + 快照 + 事件）、幂等、资料校验（原生 SQL 租户校验）、
/// 计价失败明确报错且不消耗单号、确认/取消事件时间线、跨租户拒绝。
/// </summary>
public sealed class WaybillCommandTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateOnly BusinessDate = new(2026, 11, 17);
    private static readonly decimal TotalExpected = 18.53m; // 计费重 12.35（12.345 HalfUp）× 1.5 = 18.525 → 18.53

    private Guid _tenantId;
    private Guid _originBranchId;
    private Guid _destBranchId;
    private Guid _customerId;

    private async Task SeedAsync()
    {
        var tenant = Tenant.Create($"wb-cmd-{Guid.NewGuid():N}", "开单测试租户", DateTime.UtcNow);
        await using (var tenancy = fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenant);
            await tenancy.SaveChangesAsync();
        }

        _tenantId = tenant.Id;

        await using var org = fixture.CreateOrganizationContext(_tenantId);
        var origin = Branch.Create(_tenantId, "GZ01", "广州", DateTime.UtcNow);
        var dest = Branch.Create(_tenantId, "WH02", "武汉", DateTime.UtcNow);
        org.Branches.AddRange(origin, dest);
        await org.SaveChangesAsync();
        _originBranchId = origin.Id;
        _destBranchId = dest.Id;

        await using var customers = fixture.CreateCustomersContext(_tenantId);
        var customer = Customer.Create(_tenantId, "KH001", "测试客户", DateTime.UtcNow);
        customers.Customers.Add(customer);
        await customers.SaveChangesAsync();
        _customerId = customer.Id;
    }

    /// <summary>发布一份"按重 1.5 元/kg 无条件"的规则集并发布；可选附加条件。</summary>
    private async Task<Guid> SeedPublishedRuleSetAsync(
        string code = "std", PricingCondition[]? conditions = null)
    {
        var ruleSets = new PricingRuleSetService(fixture.CreatePricingContext(_tenantId));
        var ruleSet = await ruleSets.CreateRuleSetAsync(code, "标准价卡", DateTime.UtcNow);
        var version = await ruleSets.CreateDraftVersionAsync(ruleSet.Id, new PricingRuleDefinition
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
                    Conditions = conditions ?? [],
                    Freight = new PricingFreightFeeItem
                    {
                        Name = "运费",
                        Type = PricingFreightType.ByWeight,
                        UnitPrice = 1.5m,
                    },
                    Surcharges = [],
                },
            ],
        }, DateTime.UtcNow);
        await ruleSets.PublishVersionAsync(version.Id, DateTime.UtcNow);
        return ruleSet.Id;
    }

    private WaybillService CreateService(Guid? tenantId = null) => new(
        fixture.CreateWaybillsContext(tenantId ?? _tenantId),
        new BranchSequenceService(fixture.CreatePlatformContext(tenantId ?? _tenantId)),
        new PricingTrialService(fixture.CreatePricingContext(tenantId ?? _tenantId)),
        new PricingSnapshotSerializer());

    private static CreateWaybillCommand Command(
        Guid origin, Guid dest, Guid customer, Guid ruleSetId, Guid? idempotencyKey = null) => new(
        origin, dest, customer, WaybillPaymentType.Cash,
        "张三", "13800000000", "广州市白云区XX街1号",
        "李四", "13900000000", "武汉市汉阳区YY路2号",
        [new WaybillPackageDraft("纸箱", 2, 12.345m, 0.1234m)],
        ruleSetId, BusinessDate,
        IdempotencyKey: idempotencyKey,
        BranchId: origin,
        OperatorId: Guid.NewGuid());

    [Fact]
    public async Task CreateWaybill_ShouldPersistWaybillWithPricingSnapshotAndEvent()
    {
        await SeedAsync();
        var ruleSetId = await SeedPublishedRuleSetAsync();
        var service = CreateService();

        var result = await service.CreateWaybillAsync(
            Command(_originBranchId, _destBranchId, _customerId, ruleSetId));

        // 运单号：YB + GZ01 + 业务日期间 + 序号（SEQ-001）
        Assert.Equal("YBGZ0120261117000001", result.WaybillNo);
        Assert.Equal(WaybillStatus.Draft, result.Status);
        Assert.Equal(TotalExpected, result.TotalAmount);

        await using var verification = fixture.CreateWaybillsContext(_tenantId);
        var waybill = await verification.Waybills.Include(w => w.Packages)
            .SingleAsync(w => w.Id == result.WaybillId);
        Assert.Equal(TotalExpected, waybill.TotalAmount);
        Assert.Single(waybill.Packages);
        Assert.Equal(2, waybill.Packages[0].Quantity);

        // 计价快照：版本引用 + 输入/结果 JSONB（精度往返）
        var snapshot = await verification.PricingSnapshots.SingleAsync(s => s.WaybillId == waybill.Id);
        Assert.Equal("std", snapshot.RuleSetCode);
        Assert.Equal(1, snapshot.VersionNumber);
        var serializer = new PricingSnapshotSerializer();
        var input = serializer.DeserializeInput(snapshot.InputJson);
        Assert.Equal(12.345m, input.WeightKg);
        Assert.Equal(2, input.PieceCount);
        var quote = serializer.DeserializeQuote(snapshot.QuoteJson);
        Assert.Equal(TotalExpected, quote.Total);
        Assert.Contains("单价", quote.Items[0].Detail);

        // 开单事件：→ Draft，含计价说明、操作网点与操作人
        var created = await verification.Events.SingleAsync(e => e.WaybillId == waybill.Id);
        Assert.Equal(WaybillEventType.Created, created.EventType);
        Assert.Null(created.FromStatus);
        Assert.Equal(WaybillStatus.Draft, created.ToStatus);
        Assert.Equal(_originBranchId, created.BranchId);
        Assert.NotNull(created.OperatorId);
        Assert.Contains("开单计价", created.Detail);
    }

    [Fact]
    public async Task CreateWaybill_WithSameIdempotencyKey_ShouldReturnSameWaybillWithoutDuplicates()
    {
        await SeedAsync();
        var ruleSetId = await SeedPublishedRuleSetAsync();
        var key = Guid.NewGuid();
        var command = Command(_originBranchId, _destBranchId, _customerId, ruleSetId, key);

        var first = await CreateService().CreateWaybillAsync(command);
        var second = await CreateService().CreateWaybillAsync(command);

        Assert.Equal(first.WaybillId, second.WaybillId);
        Assert.Equal(first.WaybillNo, second.WaybillNo);
        Assert.Equal(first.TotalAmount, second.TotalAmount);

        await using var verification = fixture.CreateWaybillsContext(_tenantId);
        Assert.Equal(1, await verification.Waybills.CountAsync());
        Assert.Equal(1, await verification.PricingSnapshots.CountAsync());
        Assert.Equal(1, await verification.Events.CountAsync());
    }

    [Fact]
    public async Task CreateWaybill_WithDisabledOriginBranch_ShouldBeRejected()
    {
        await SeedAsync();
        var ruleSetId = await SeedPublishedRuleSetAsync();

        await using (var org = fixture.CreateOrganizationContext(_tenantId))
        {
            var branch = await org.Branches.SingleAsync(b => b.Id == _originBranchId);
            branch.Disable(DateTime.UtcNow);
            await org.SaveChangesAsync();
        }

        var ex = await Assert.ThrowsAsync<WaybillDomainException>(
            () => CreateService().CreateWaybillAsync(
                Command(_originBranchId, _destBranchId, _customerId, ruleSetId)));
        Assert.Contains("已停用", ex.Message);

        // 不产生任何数据
        await using var verification = fixture.CreateWaybillsContext(_tenantId);
        Assert.Equal(0, await verification.Waybills.CountAsync());
    }

    [Fact]
    public async Task CreateWaybill_WithOtherTenantCustomer_ShouldBeRejected()
    {
        await SeedAsync();
        var tenantB = Tenant.Create($"wb-cmd-b-{Guid.NewGuid():N}", "租户乙", DateTime.UtcNow);
        await using (var tenancy = fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenantB);
            await tenancy.SaveChangesAsync();
        }

        var ruleSetId = await SeedPublishedRuleSetAsync();

        // 租户乙 seed 自己的网点（复合外键要求），仅客户引用租户甲 → 原生 SQL 租户校验拒绝
        await using var orgB = fixture.CreateOrganizationContext(tenantB.Id);
        var originB = Branch.Create(tenantB.Id, "GZ01", "广州", DateTime.UtcNow);
        var destB = Branch.Create(tenantB.Id, "WH02", "武汉", DateTime.UtcNow);
        orgB.Branches.AddRange(originB, destB);
        await orgB.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<EntityNotFoundException>(
            () => CreateService(tenantB.Id).CreateWaybillAsync(
                Command(originB.Id, destB.Id, _customerId, ruleSetId)));
        Assert.Contains("客户", ex.Message);
    }

    [Fact]
    public async Task CreateWaybill_WithoutPublishedVersion_ShouldBeRejectedWithoutConsumingNumber()
    {
        await SeedAsync();
        var ruleSets = new PricingRuleSetService(fixture.CreatePricingContext(_tenantId));
        var ruleSet = await ruleSets.CreateRuleSetAsync("draft-only", "仅草稿", DateTime.UtcNow);
        await ruleSets.CreateDraftVersionAsync(ruleSet.Id, new PricingRuleDefinition
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
        }, DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<PricingRuleViolationException>(
            () => CreateService().CreateWaybillAsync(
                Command(_originBranchId, _destBranchId, _customerId, ruleSet.Id)));
        Assert.Contains("没有已发布", ex.Message);

        // 先试算后取号：失败不消耗单号
        await using var platform = fixture.CreatePlatformContext(_tenantId);
        Assert.Equal(0, await platform.Sequences.CountAsync());
        await using var verification = fixture.CreateWaybillsContext(_tenantId);
        Assert.Equal(0, await verification.Waybills.CountAsync());
    }

    [Fact]
    public async Task CreateWaybill_WithNoMatchingRule_ShouldBeRejectedExplicitly()
    {
        await SeedAsync();
        // 规则条件：重量 > 100kg；开单输入 12.345kg → 无匹配 → 明确报错（不静默计零）
        var ruleSetId = await SeedPublishedRuleSetAsync(conditions:
        [
            new PricingCondition
            {
                Field = PricingConditionField.WeightKg,
                Operator = PricingConditionOperator.GreaterThan,
                Value = "100",
            },
        ]);

        var ex = await Assert.ThrowsAsync<PricingMatchFailedException>(
            () => CreateService().CreateWaybillAsync(
                Command(_originBranchId, _destBranchId, _customerId, ruleSetId)));
        Assert.Contains("没有命中", ex.Message);

        await using var verification = fixture.CreateWaybillsContext(_tenantId);
        Assert.Equal(0, await verification.Waybills.CountAsync());
    }

    [Fact]
    public async Task ConfirmAndCancel_ShouldTransitionStatusAndRecordEvents()
    {
        await SeedAsync();
        var ruleSetId = await SeedPublishedRuleSetAsync();
        var service = CreateService();
        var result = await service.CreateWaybillAsync(
            Command(_originBranchId, _destBranchId, _customerId, ruleSetId));

        // 确认：Draft → Confirmed + 事件（每次验证用新鲜 context，避免 identity map 旧快照）
        await service.ConfirmWaybillAsync(result.WaybillId, branchId: _originBranchId, operatorId: Guid.NewGuid());
        await using (var verification = fixture.CreateWaybillsContext(_tenantId))
        {
            var confirmed = await verification.Waybills.AsNoTracking()
                .SingleAsync(w => w.Id == result.WaybillId);
            Assert.Equal(WaybillStatus.Confirmed, confirmed.Status);
        }

        // 重复确认拒绝（WB-001 状态机）
        await Assert.ThrowsAsync<WaybillDomainException>(
            () => service.ConfirmWaybillAsync(result.WaybillId));

        // 取消：Confirmed → Cancelled + 事件（未发运可取消）
        await service.CancelWaybillAsync(result.WaybillId, "客户取消", _originBranchId, Guid.NewGuid());
        await using (var verification = fixture.CreateWaybillsContext(_tenantId))
        {
            var cancelled = await verification.Waybills.AsNoTracking()
                .SingleAsync(w => w.Id == result.WaybillId);
            Assert.Equal(WaybillStatus.Cancelled, cancelled.Status);
        }

        // 终态：再取消拒绝
        await Assert.ThrowsAsync<WaybillDomainException>(
            () => service.CancelWaybillAsync(result.WaybillId));

        // 事件时间线：开单 → 确认 → 取消，前后状态正确，取消含原因
        var events = await service.GetEventsAsync(result.WaybillId);
        Assert.Equal(3, events.Count);
        Assert.Equal(
            [WaybillEventType.Created, WaybillEventType.Confirmed, WaybillEventType.Cancelled],
            events.Select(e => e.EventType).ToArray());
        Assert.Null(events[0].FromStatus);
        Assert.Equal(WaybillStatus.Draft, events[1].FromStatus);
        Assert.Equal(WaybillStatus.Confirmed, events[1].ToStatus);
        Assert.Equal(WaybillStatus.Confirmed, events[2].FromStatus);
        Assert.Equal(WaybillStatus.Cancelled, events[2].ToStatus);
        Assert.Contains("客户取消", events[2].Detail);
    }

    [Fact]
    public async Task ConfirmWaybill_AfterCancelled_ShouldBeRejected()
    {
        await SeedAsync();
        var ruleSetId = await SeedPublishedRuleSetAsync();
        var service = CreateService();
        var result = await service.CreateWaybillAsync(
            Command(_originBranchId, _destBranchId, _customerId, ruleSetId));
        await service.CancelWaybillAsync(result.WaybillId);

        await Assert.ThrowsAsync<WaybillDomainException>(
            () => service.ConfirmWaybillAsync(result.WaybillId));
    }

    [Fact]
    public async Task ConfirmWaybill_CrossTenant_ShouldBeEntityNotFound()
    {
        await SeedAsync();
        var ruleSetId = await SeedPublishedRuleSetAsync();
        var result = await CreateService().CreateWaybillAsync(
            Command(_originBranchId, _destBranchId, _customerId, ruleSetId));

        var tenantB = Tenant.Create($"wb-cmd-b-{Guid.NewGuid():N}", "租户乙", DateTime.UtcNow);
        await using (var tenancy = fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenantB);
            await tenancy.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => CreateService(tenantB.Id).ConfirmWaybillAsync(result.WaybillId));
    }
}
