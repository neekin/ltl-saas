using Ltl.Modules.Pricing.Domain;
using Xunit;

namespace Ltl.UnitTests.Pricing;

/// <summary>
/// PRICE-002 执行器测试：匹配语义、计费数量策略、分段/最低收费、附加费/折扣、舍入边界、确定性。
/// </summary>
public class PricingEngineTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid OtherCustomerId = Guid.NewGuid();

    private static PricingQuoteInput Input(
        decimal weight = 0m,
        decimal volume = 0m,
        int pieces = 1,
        Guid? customer = null,
        Guid? route = null,
        Guid? origin = null,
        Guid? dest = null,
        decimal? declaredValue = null) => new()
        {
            WeightKg = weight,
            VolumeM3 = volume,
            PieceCount = pieces,
            CustomerId = customer,
            RouteId = route,
            OriginBranchId = origin,
            DestBranchId = dest,
            DeclaredValue = declaredValue,
        };

    private static PricingFreightFeeItem ByWeight(decimal unitPrice, decimal? minCharge = null) => new()
    {
        Name = "运费",
        Type = PricingFreightType.ByWeight,
        UnitPrice = unitPrice,
        MinCharge = minCharge,
    };

    private static PricingRule Rule(
        int priority = 1,
        bool enabled = true,
        PricingCondition[]? conditions = null,
        PricingFreightFeeItem? freight = null,
        BillingWeightStrategy strategy = BillingWeightStrategy.WeightOnly,
        decimal? factor = null,
        PricingSurchargeFeeItem[]? surcharges = null,
        PricingDiscountFeeItem? discount = null) => new()
        {
            Priority = priority,
            Name = $"规则-{priority}",
            Enabled = enabled,
            BillingWeightStrategy = strategy,
            VolumeToWeightFactor = factor,
            Conditions = conditions ?? [],
            Freight = freight ?? ByWeight(1.5m),
            Surcharges = surcharges ?? [],
            Discount = discount,
        };

    private static PricingRuleDefinition Definition(params PricingRule[] rules) => new()
    {
        Rounding = RoundingPolicy.HalfUpTwoDecimals,
        Rules = rules,
    };

    private static PricingQuoteResult Calculate(
        PricingRuleDefinition definition, PricingQuoteInput input) =>
        PricingEngine.Calculate(definition, input);

    // ---------- 匹配 ----------

    [Fact]
    public void FirstMatchByPriority_ShouldWin()
    {
        var definition = Definition(
            Rule(priority: 2, freight: ByWeight(9m)),
            Rule(priority: 1, freight: ByWeight(1m)));

        var quote = Calculate(definition, Input(weight: 10m));

        Assert.Equal(1, quote.MatchedRulePriority);
        Assert.Equal(10m, quote.Total); // 单价 1 × 10kg
    }

    [Fact]
    public void DisabledRule_ShouldBeSkipped()
    {
        var definition = Definition(
            Rule(priority: 1, enabled: false, freight: ByWeight(9m)),
            Rule(priority: 2, freight: ByWeight(1m)));

        var quote = Calculate(definition, Input(weight: 10m));

        Assert.Equal(2, quote.MatchedRulePriority);
    }

    [Fact]
    public void RuleWithoutConditions_ShouldMatchAnyInput()
    {
        var quote = Calculate(Definition(Rule()), Input(weight: 5m));
        Assert.Equal(1, quote.MatchedRulePriority);
    }

    [Fact]
    public void CustomerCondition_WithoutCustomerInInput_ShouldNotMatch()
    {
        var definition = Definition(
            Rule(priority: 1, conditions:
            [
                new PricingCondition
                {
                    Field = PricingConditionField.CustomerId,
                    Operator = PricingConditionOperator.Equals,
                    Value = CustomerId.ToString("D"),
                },
            ]),
            Rule(priority: 2, freight: ByWeight(2m)));

        // 输入缺客户 → 优先级 1 不命中，落到默认规则 2
        var quote = Calculate(definition, Input(weight: 5m));
        Assert.Equal(2, quote.MatchedRulePriority);

        // 携带客户 → 命中优先级 1
        var matched = Calculate(definition, Input(weight: 5m, customer: CustomerId));
        Assert.Equal(1, matched.MatchedRulePriority);
    }

    [Fact]
    public void InCondition_ShouldMatchAnyListedValue()
    {
        var conditions = new[]
        {
            new PricingCondition
            {
                Field = PricingConditionField.CustomerId,
                Operator = PricingConditionOperator.In,
                Value = $"{OtherCustomerId:D},{CustomerId:D}",
            },
        };

        var quote = Calculate(
            Definition(Rule(conditions: conditions)), Input(weight: 5m, customer: CustomerId));

        Assert.Equal(1, quote.MatchedRulePriority);
    }

    [Fact]
    public void Between_ShouldBeInclusiveOnBothBounds()
    {
        var conditions = new[]
        {
            new PricingCondition
            {
                Field = PricingConditionField.WeightKg,
                Operator = PricingConditionOperator.Between,
                Value = "1",
                Value2 = "3.35",
            },
        };
        var definition = Definition(Rule(conditions: conditions));

        Assert.NotNull(Calculate(definition, Input(weight: 1m)));
        Assert.NotNull(Calculate(definition, Input(weight: 3.35m)));
        Assert.Throws<PricingMatchFailedException>(() => Calculate(definition, Input(weight: 3.36m)));
    }

    [Fact]
    public void GreaterOrEqual_BoundaryValue_ShouldMatch()
    {
        var conditions = new[]
        {
            new PricingCondition
            {
                Field = PricingConditionField.WeightKg,
                Operator = PricingConditionOperator.GreaterOrEqual,
                Value = "30",
            },
        };
        var definition = Definition(Rule(conditions: conditions));

        Assert.NotNull(Calculate(definition, Input(weight: 30m)));
        Assert.Throws<PricingMatchFailedException>(() => Calculate(definition, Input(weight: 29.99m)));
    }

    [Fact]
    public void NoMatchingRule_ShouldThrowExplicitError()
    {
        var definition = Definition(Rule(conditions:
        [
            new PricingCondition
            {
                Field = PricingConditionField.WeightKg,
                Operator = PricingConditionOperator.GreaterThan,
                Value = "100",
            },
        ]));

        var ex = Assert.Throws<PricingMatchFailedException>(
            () => Calculate(definition, Input(weight: 5m)));
        Assert.Contains("没有命中", ex.Message);
    }

    // ---------- 计费数量 ----------

    [Fact]
    public void WeightOnly_ShouldUseWeight()
    {
        var quote = Calculate(Definition(Rule(freight: ByWeight(2m))), Input(weight: 3.35m));
        Assert.Equal(3.35m, quote.BillingQuantity);
        Assert.Equal(6.7m, quote.Total);
    }

    [Fact]
    public void VolumeOnly_ShouldUseVolume()
    {
        var quote = Calculate(Definition(Rule(
            strategy: BillingWeightStrategy.VolumeOnly,
            freight: new PricingFreightFeeItem
            {
                Name = "运费",
                Type = PricingFreightType.ByVolume,
                UnitPrice = 80m,
            })), Input(volume: 1.234m));

        Assert.Equal(1.23m, quote.BillingQuantity); // 2 位 HalfUp
        Assert.Equal(98.4m, quote.Total);
    }

    [Fact]
    public void PieceCountStrategy_ShouldUsePieces()
    {
        var quote = Calculate(Definition(Rule(
            strategy: BillingWeightStrategy.PieceCount,
            freight: new PricingFreightFeeItem
            {
                Name = "运费",
                Type = PricingFreightType.ByPieces,
                UnitPrice = 12m,
            })), Input(pieces: 4));

        Assert.Equal(4m, quote.BillingQuantity);
        Assert.Equal(48m, quote.Total);
    }

    [Fact]
    public void WeightVolumeMax_ShouldUseLargerOfWeightAndVolumeWeight()
    {
        // 体积重 = 0.01 × 333.33 = 3.3333 > 重量 3.00
        var quote = Calculate(Definition(Rule(
            strategy: BillingWeightStrategy.WeightVolumeMax,
            factor: 333.33m,
            freight: ByWeight(2m))), Input(weight: 3m, volume: 0.01m));

        Assert.Equal(3.33m, quote.BillingQuantity);
        Assert.Equal(6.66m, quote.Total);
    }

    [Fact]
    public void WeightVolumeMin_ShouldUseSmallerOfWeightAndVolumeWeight()
    {
        var quote = Calculate(Definition(Rule(
            strategy: BillingWeightStrategy.WeightVolumeMin,
            factor: 333.33m,
            freight: ByWeight(2m))), Input(weight: 3m, volume: 0.01m));

        Assert.Equal(3m, quote.BillingQuantity); // 重量 3.00 < 体积重 3.3333
        Assert.Equal(6m, quote.Total);
    }

    // ---------- 运费 ----------

    [Fact]
    public void FixedFreight_ShouldReturnConfiguredAmount()
    {
        var quote = Calculate(Definition(Rule(freight: new PricingFreightFeeItem
        {
            Name = "运费",
            Type = PricingFreightType.Fixed,
            Amount = 120m,
        })), Input(weight: 10m, pieces: 3));

        Assert.Null(quote.BillingQuantity);
        Assert.Equal(120m, quote.Total);
        Assert.Contains("固定运费", quote.Items[0].Detail);
    }

    [Fact]
    public void TieredFreight_ShouldSpanMultipleSegments()
    {
        // 3.35kg：首档 [0,1) 1×18=18；次档 [1,∞) 2.35×4.2=9.87；合计 27.87
        var quote = Calculate(Definition(Rule(freight: TieredFreight([
            new PricingTier { UpTo = 1m, UnitPrice = 18m },
            new PricingTier { UpTo = null, UnitPrice = 4.2m },
        ]))), Input(weight: 3.35m));

        Assert.Equal(27.87m, quote.Total);
        Assert.Contains("[0,1)", quote.Items[0].Detail);
        Assert.Contains("[1,∞)", quote.Items[0].Detail);
    }

    [Fact]
    public void TieredFreight_ExactTierBoundary_ShouldNotSpillIntoNextTier()
    {
        var quote = Calculate(Definition(Rule(freight: TieredFreight([
            new PricingTier { UpTo = 1m, UnitPrice = 18m },
            new PricingTier { UpTo = null, UnitPrice = 4.2m },
        ]))), Input(weight: 1m));

        Assert.Equal(18m, quote.Total);
    }

    [Fact]
    public void MinCharge_ShouldApplyWhenCalculationBelowFloor()
    {
        var quote = Calculate(Definition(Rule(freight: ByWeight(2m, minCharge: 35m))), Input(weight: 0.5m));
        Assert.Equal(35m, quote.Total); // 1 元 < 35 → 按最低收费
        Assert.Contains("最低收费", quote.Items[0].Detail);
    }

    [Fact]
    public void MinCharge_ShouldNotApplyWhenCalculationAboveFloor()
    {
        var quote = Calculate(Definition(Rule(freight: ByWeight(2m, minCharge: 35m))), Input(weight: 50m));
        Assert.Equal(100m, quote.Total);
    }

    // ---------- 附加费 ----------

    [Fact]
    public void PerWaybillSurcharge_ShouldAddFixedAmount()
    {
        var quote = Calculate(Definition(Rule(
            freight: ByWeight(1m),
            surcharges:
            [
                new PricingSurchargeFeeItem { Name = "提货费", Basis = PricingSurchargeBasis.PerWaybill, Amount = 30m },
            ])), Input(weight: 10m));

        Assert.Equal(40m, quote.Total);
        Assert.Equal(2, quote.Items.Count);
    }

    [Fact]
    public void PerPieceSurcharge_ShouldMultiplyByPieces()
    {
        var quote = Calculate(Definition(Rule(
            freight: ByWeight(1m),
            surcharges:
            [
                new PricingSurchargeFeeItem { Name = "包装费", Basis = PricingSurchargeBasis.PerPiece, Amount = 2m },
            ])), Input(weight: 10m, pieces: 3));

        Assert.Equal(16m, quote.Total); // 10 + 2×3
    }

    [Fact]
    public void InsuranceSurcharge_ShouldUseDeclaredValueRate()
    {
        // 声明价值 10000 元 × 0.5% = 50 元保价费；运费 10×1.5=15，合计 65
        var quote = Calculate(Definition(Rule(
            freight: ByWeight(1.5m),
            surcharges:
            [
                new PricingSurchargeFeeItem { Name = "保价费", Basis = PricingSurchargeBasis.PerDeclaredValue, Amount = 0m, Rate = 0.5m },
            ])), Input(weight: 10m, declaredValue: 10000m));

        Assert.Equal(65m, quote.Total);
        Assert.Equal(50m, quote.InsuranceFee);
        var insurance = Assert.Single(quote.Items, i => i.Name == "保价费");
        Assert.Contains("声明价值 10000 元 × 0.5%", insurance.Detail);
    }

    [Fact]
    public void InsuranceSurcharge_WithoutDeclaredValue_ShouldChargeZero()
    {
        var quote = Calculate(Definition(Rule(
            freight: ByWeight(1.5m),
            surcharges:
            [
                new PricingSurchargeFeeItem { Name = "保价费", Basis = PricingSurchargeBasis.PerDeclaredValue, Amount = 0m, Rate = 0.5m },
            ])), Input(weight: 10m));

        Assert.Equal(15m, quote.Total); // 仅运费
        Assert.Equal(0m, quote.InsuranceFee);
    }

    // ---------- 折扣 ----------

    [Fact]
    public void RateDiscount_ShouldApplyToSubtotal()
    {
        // 小计 27.87，5% 折扣 = 1.3935 → 1.39，总额 26.48
        var quote = Calculate(Definition(Rule(
            freight: TieredFreight([
                new PricingTier { UpTo = 1m, UnitPrice = 18m },
                new PricingTier { UpTo = null, UnitPrice = 4.2m },
            ]),
            discount: new PricingDiscountFeeItem
            {
                Name = "大客户折扣",
                Mode = PricingDiscountMode.Rate,
                Rate = 5m,
            })), Input(weight: 3.35m));

        Assert.Equal(27.87m, quote.Subtotal);
        Assert.Equal(1.39m, quote.Discount!.Amount);
        Assert.Equal(26.48m, quote.Total);
    }

    [Fact]
    public void AmountDiscount_ShouldSubtractFixedReduction()
    {
        var quote = Calculate(Definition(Rule(
            freight: ByWeight(1m),
            discount: new PricingDiscountFeeItem
            {
                Name = "限时减免",
                Mode = PricingDiscountMode.Amount,
                Amount = 3m,
            })), Input(weight: 10m));

        Assert.Equal(7m, quote.Total);
    }

    [Fact]
    public void Discount_ShouldNeverDriveTotalBelowZero()
    {
        var quote = Calculate(Definition(Rule(
            freight: ByWeight(1m),
            discount: new PricingDiscountFeeItem
            {
                Name = "全免促销",
                Mode = PricingDiscountMode.Amount,
                Amount = 999m,
            })), Input(weight: 1m));

        Assert.Equal(0m, quote.Total);
    }

    // ---------- 舍入与确定性 ----------

    [Fact]
    public void Rounding_ShouldUseHalfUpOnMidpoint()
    {
        // 1.5 × 0.67 = 1.005 → HalfUp → 1.01（不是银行家舍入的 1.00）
        var quote = Calculate(Definition(Rule(freight: ByWeight(1.5m))), Input(weight: 0.67m));
        Assert.Equal(1.01m, quote.Total);
    }

    [Fact]
    public void SameInputAndDefinition_ShouldProduceIdenticalResults()
    {
        var definition = Definition(Rule(
            freight: TieredFreight([
                new PricingTier { UpTo = 1m, UnitPrice = 18m },
                new PricingTier { UpTo = null, UnitPrice = 4.2m },
            ]),
            surcharges:
            [
                new PricingSurchargeFeeItem { Name = "提货费", Basis = PricingSurchargeBasis.PerPiece, Amount = 1.5m },
            ],
            discount: new PricingDiscountFeeItem { Name = "折扣", Mode = PricingDiscountMode.Rate, Rate = 7.5m }));
        var input = Input(weight: 3.35m, pieces: 2);

        var first = Calculate(definition, input);
        var second = Calculate(definition, input);

        Assert.Equal(first.Total, second.Total);
        Assert.Equal(first.Subtotal, second.Subtotal);
        Assert.Equal(first.Items.Count, second.Items.Count);
    }

    // ---------- 输入校验 ----------

    [Fact]
    public void NegativeWeight_ShouldBeRejected()
    {
        Assert.Throws<ArgumentException>(
            () => Calculate(Definition(Rule()), Input(weight: -1m)));
    }

    [Fact]
    public void ZeroPieces_ShouldBeRejected()
    {
        Assert.Throws<ArgumentException>(
            () => Calculate(Definition(Rule()), Input(pieces: 0)));
    }

    private static PricingFreightFeeItem TieredFreight(IReadOnlyList<PricingTier> tiers) => new()
    {
        Name = "运费",
        Type = PricingFreightType.Tiered,
        Tiers = tiers,
    };
}
