using Ltl.Modules.Pricing.Domain;
using Xunit;

namespace Ltl.UnitTests.Pricing;

/// <summary>
/// PRICE-001 语义校验器测试：白名单字段/操作符兼容、数值解析、区间、阶梯、金额边界、折扣。
/// </summary>
public class PricingDefinitionValidatorTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid RouteId = Guid.NewGuid();

    /// <summary>最小合法规则（按重计费，无条件，无附加费/折扣），测试按需整体替换属性。</summary>
    private static PricingRule Rule(int priority = 1, string name = "标准运价") => new()
    {
        Priority = priority,
        Name = name,
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
    };

    private static PricingRuleDefinition Definition(params PricingRule[] rules) => new()
    {
        Rounding = RoundingPolicy.HalfUpTwoDecimals,
        Rules = rules.Length == 0 ? [Rule()] : rules,
    };

    private static IReadOnlyList<string> Validate(PricingRuleDefinition definition) =>
        PricingDefinitionValidator.Validate(definition);

    private static string SingleError(PricingRule rule)
    {
        var errors = Validate(Definition(rule));
        return Assert.Single(errors);
    }

    [Fact]
    public void ValidRichDefinition_ShouldHaveZeroErrors()
    {
        var definition = new PricingRuleDefinition
        {
            Rounding = RoundingPolicy.HalfUpTwoDecimals,
            Rules =
            [
                new PricingRule
                {
                    Priority = 1,
                    Name = "重货",
                    Enabled = true,
                    BillingWeightStrategy = BillingWeightStrategy.WeightVolumeMax,
                    VolumeToWeightFactor = 333.33m,
                    Conditions =
                    [
                        new PricingCondition
                        {
                            Field = PricingConditionField.CustomerId,
                            Operator = PricingConditionOperator.Equals,
                            Value = CustomerId.ToString("D"),
                        },
                    ],
                    Freight = new PricingFreightFeeItem
                    {
                        Name = "运费",
                        Type = PricingFreightType.Tiered,
                        MinCharge = 35m,
                        Tiers =
                        [
                            new PricingTier { UpTo = 1m, UnitPrice = 18m },
                            new PricingTier { UpTo = null, UnitPrice = 4.2m },
                        ],
                    },
                    Surcharges =
                    [
                        new PricingSurchargeFeeItem { Name = "提货费", Basis = PricingSurchargeBasis.PerWaybill, Amount = 30m },
                        new PricingSurchargeFeeItem { Name = "包装费", Basis = PricingSurchargeBasis.PerPiece, Amount = 2m },
                    ],
                    Discount = new PricingDiscountFeeItem
                    {
                        Name = "大客户折扣",
                        Mode = PricingDiscountMode.Rate,
                        Rate = 5m,
                    },
                },
            ],
        };

        Assert.Empty(Validate(definition));
    }

    [Fact]
    public void ValidFixedFreight_WithBetweenAndInConditions_ShouldHaveZeroErrors()
    {
        var definition = new PricingRuleDefinition
        {
            Rounding = RoundingPolicy.HalfUpTwoDecimals,
            Rules =
            [
                new PricingRule
                {
                    Priority = 2,
                    Name = "轻抛专线",
                    Enabled = false,
                    BillingWeightStrategy = BillingWeightStrategy.VolumeOnly,
                    Conditions =
                    [
                        new PricingCondition
                        {
                            Field = PricingConditionField.VolumeM3,
                            Operator = PricingConditionOperator.Between,
                            Value = "0.5",
                            Value2 = "3",
                        },
                        new PricingCondition
                        {
                            Field = PricingConditionField.RouteId,
                            Operator = PricingConditionOperator.In,
                            Value = $"{RouteId.ToString("D")},{Guid.NewGuid():D}",
                        },
                        new PricingCondition
                        {
                            Field = PricingConditionField.PieceCount,
                            Operator = PricingConditionOperator.LessOrEqual,
                            Value = "10",
                        },
                    ],
                    Freight = new PricingFreightFeeItem
                    {
                        Name = "运费",
                        Type = PricingFreightType.Fixed,
                        Amount = 120m,
                    },
                    Surcharges = [],
                    Discount = new PricingDiscountFeeItem
                    {
                        Name = "限时减免",
                        Mode = PricingDiscountMode.Amount,
                        Amount = 10m,
                    },
                },
            ],
        };

        Assert.Empty(Validate(definition));
    }

    [Fact]
    public void EmptyRules_ShouldError()
    {
        var errors = Validate(new PricingRuleDefinition
        {
            Rounding = RoundingPolicy.HalfUpTwoDecimals,
            Rules = [],
        });

        Assert.Contains(errors, e => e.Contains("至少 1 条规则"));
    }

    [Fact]
    public void UnsupportedRounding_ShouldError()
    {
        var errors = Validate(new PricingRuleDefinition
        {
            Rounding = (RoundingPolicy)99,
            Rules = [Rule()],
        });

        Assert.Contains(errors, e => e.Contains("舍入策略"));
    }

    [Fact]
    public void DuplicatePriority_ShouldError()
    {
        var errors = Validate(Definition(Rule(), Rule()));
        Assert.Contains(errors, e => e.Contains("优先级在集合内重复"));
    }

    [Fact]
    public void ZeroPriority_ShouldError()
    {
        var error = SingleError(new PricingRule
        {
            Priority = 0,
            Name = "规则",
            Enabled = true,
            BillingWeightStrategy = BillingWeightStrategy.WeightOnly,
            Conditions = [],
            Freight = new PricingFreightFeeItem { Name = "运费", Type = PricingFreightType.ByWeight, UnitPrice = 1m },
            Surcharges = [],
        });

        Assert.Contains("优先级必须 ≥1", error);
    }

    [Fact]
    public void MaxStrategyWithoutFactor_ShouldError()
    {
        var rule = Rule();
        var error = SingleError(new PricingRule
        {
            Priority = rule.Priority,
            Name = rule.Name,
            Enabled = true,
            BillingWeightStrategy = BillingWeightStrategy.WeightVolumeMax,
            Conditions = [],
            Freight = rule.Freight,
            Surcharges = [],
        });

        Assert.Contains("体积重系数", error);
    }

    [Fact]
    public void WeightOnlyWithFactor_ShouldError()
    {
        var rule = Rule();
        var error = SingleError(new PricingRule
        {
            Priority = rule.Priority,
            Name = rule.Name,
            Enabled = true,
            BillingWeightStrategy = BillingWeightStrategy.WeightOnly,
            VolumeToWeightFactor = 300m,
            Conditions = [],
            Freight = rule.Freight,
            Surcharges = [],
        });

        Assert.Contains("不使用体积重系数", error);
    }

    [Theory]
    [InlineData(PricingConditionField.CustomerId, PricingConditionOperator.GreaterThan, "仅支持 Equals/In")]
    [InlineData(PricingConditionField.WeightKg, PricingConditionOperator.Equals, "不支持 Equals/In")]
    [InlineData(PricingConditionField.WeightKg, PricingConditionOperator.Between, "必须提供合法的非负上界")]
    public void IncompatibleFieldOperatorCombination_ShouldError(
        PricingConditionField field, PricingConditionOperator op, string expectedFragment)
    {
        var rule = Rule();
        var error = SingleError(new PricingRule
        {
            Priority = rule.Priority,
            Name = rule.Name,
            Enabled = true,
            BillingWeightStrategy = BillingWeightStrategy.WeightOnly,
            Conditions =
            [
                new PricingCondition { Field = field, Operator = op, Value = "1" },
            ],
            Freight = rule.Freight,
            Surcharges = [],
        });

        Assert.Contains(expectedFragment, error);
    }

    [Fact]
    public void UnparsableNumericValue_ShouldError()
    {
        var error = SingleError(new PricingRule
        {
            Priority = 1,
            Name = "规则",
            Enabled = true,
            BillingWeightStrategy = BillingWeightStrategy.WeightOnly,
            Conditions =
            [
                new PricingCondition
                {
                    Field = PricingConditionField.WeightKg,
                    Operator = PricingConditionOperator.GreaterThan,
                    Value = "abc",
                },
            ],
            Freight = new PricingFreightFeeItem { Name = "运费", Type = PricingFreightType.ByWeight, UnitPrice = 1m },
            Surcharges = [],
        });

        Assert.Contains("不是合法的非负数", error);
    }

    [Fact]
    public void FractionalPieceCount_ShouldError()
    {
        var error = SingleError(new PricingRule
        {
            Priority = 1,
            Name = "规则",
            Enabled = true,
            BillingWeightStrategy = BillingWeightStrategy.WeightOnly,
            Conditions =
            [
                new PricingCondition
                {
                    Field = PricingConditionField.PieceCount,
                    Operator = PricingConditionOperator.GreaterOrEqual,
                    Value = "2.5",
                },
            ],
            Freight = new PricingFreightFeeItem { Name = "运费", Type = PricingFreightType.ByWeight, UnitPrice = 1m },
            Surcharges = [],
        });

        Assert.Contains("件数必须为整数", error);
    }

    [Fact]
    public void InvalidGuidValue_ShouldError()
    {
        var error = SingleError(new PricingRule
        {
            Priority = 1,
            Name = "规则",
            Enabled = true,
            BillingWeightStrategy = BillingWeightStrategy.WeightOnly,
            Conditions =
            [
                new PricingCondition
                {
                    Field = PricingConditionField.DestBranchId,
                    Operator = PricingConditionOperator.In,
                    Value = "not-a-guid",
                },
            ],
            Freight = new PricingFreightFeeItem { Name = "运费", Type = PricingFreightType.ByWeight, UnitPrice = 1m },
            Surcharges = [],
        });

        Assert.Contains("不是合法的 Guid", error);
    }

    [Fact]
    public void BetweenWithReversedBounds_ShouldError()
    {
        var error = SingleError(new PricingRule
        {
            Priority = 1,
            Name = "规则",
            Enabled = true,
            BillingWeightStrategy = BillingWeightStrategy.WeightOnly,
            Conditions =
            [
                new PricingCondition
                {
                    Field = PricingConditionField.WeightKg,
                    Operator = PricingConditionOperator.Between,
                    Value = "30",
                    Value2 = "20",
                },
            ],
            Freight = new PricingFreightFeeItem { Name = "运费", Type = PricingFreightType.ByWeight, UnitPrice = 1m },
            Surcharges = [],
        });

        Assert.Contains("下界必须小于上界", error);
    }

    [Fact]
    public void Value2OnNonBetweenOperator_ShouldError()
    {
        var error = SingleError(new PricingRule
        {
            Priority = 1,
            Name = "规则",
            Enabled = true,
            BillingWeightStrategy = BillingWeightStrategy.WeightOnly,
            Conditions =
            [
                new PricingCondition
                {
                    Field = PricingConditionField.WeightKg,
                    Operator = PricingConditionOperator.LessThan,
                    Value = "30",
                    Value2 = "40",
                },
            ],
            Freight = new PricingFreightFeeItem { Name = "运费", Type = PricingFreightType.ByWeight, UnitPrice = 1m },
            Surcharges = [],
        });

        Assert.Contains("仅 Between 使用上界值", error);
    }

    [Fact]
    public void FixedFreightWithZeroAmount_ShouldError()
    {
        var error = SingleError(new PricingRule
        {
            Priority = 1,
            Name = "规则",
            Enabled = true,
            BillingWeightStrategy = BillingWeightStrategy.WeightOnly,
            Conditions = [],
            Freight = new PricingFreightFeeItem { Name = "运费", Type = PricingFreightType.Fixed, Amount = 0m },
            Surcharges = [],
        });

        Assert.Contains("固定运费金额必须 >0", error);
    }

    [Fact]
    public void FixedFreightWithUnitPrice_ShouldError()
    {
        var error = SingleError(new PricingRule
        {
            Priority = 1,
            Name = "规则",
            Enabled = true,
            BillingWeightStrategy = BillingWeightStrategy.WeightOnly,
            Conditions = [],
            Freight = new PricingFreightFeeItem
            {
                Name = "运费",
                Type = PricingFreightType.Fixed,
                Amount = 100m,
                UnitPrice = 1m,
            },
            Surcharges = [],
        });

        Assert.Contains("不支持单价/阶梯字段", error);
    }

    [Fact]
    public void ByWeightWithoutUnitPrice_ShouldError()
    {
        var error = SingleError(new PricingRule
        {
            Priority = 1,
            Name = "规则",
            Enabled = true,
            BillingWeightStrategy = BillingWeightStrategy.WeightOnly,
            Conditions = [],
            Freight = new PricingFreightFeeItem { Name = "运费", Type = PricingFreightType.ByWeight },
            Surcharges = [],
        });

        Assert.Contains("单价必须 >0", error);
    }

    [Fact]
    public void NegativeMinCharge_ShouldError()
    {
        var error = SingleError(new PricingRule
        {
            Priority = 1,
            Name = "规则",
            Enabled = true,
            BillingWeightStrategy = BillingWeightStrategy.WeightOnly,
            Conditions = [],
            Freight = new PricingFreightFeeItem
            {
                Name = "运费",
                Type = PricingFreightType.ByWeight,
                UnitPrice = 1.5m,
                MinCharge = -1m,
            },
            Surcharges = [],
        });

        Assert.Contains("最低收费必须 ≥0", error);
    }

    [Fact]
    public void TieredWithoutLastUnbounded_ShouldError()
    {
        var error = SingleError(TieredRule([
            new PricingTier { UpTo = 1m, UnitPrice = 18m },
            new PricingTier { UpTo = 5m, UnitPrice = 4m },
        ]));

        Assert.Contains("末档必须无上限", error);
    }

    [Fact]
    public void TieredWithNonAscendingBounds_ShouldError()
    {
        var error = SingleError(TieredRule([
            new PricingTier { UpTo = 5m, UnitPrice = 18m },
            new PricingTier { UpTo = 3m, UnitPrice = 4m },
            new PricingTier { UpTo = null, UnitPrice = 3m },
        ]));

        Assert.Contains("严格大于上一档上界", error);
    }

    [Fact]
    public void TieredWithUnboundedMiddleTier_ShouldError()
    {
        var errors = Validate(Definition(TieredRule([
            new PricingTier { UpTo = 1m, UnitPrice = 18m },
            new PricingTier { UpTo = null, UnitPrice = 4m },
            new PricingTier { UpTo = 10m, UnitPrice = 3m },
        ])));

        // 中间档无界 + 末档有界各报一条
        Assert.Contains(errors, e => e.Contains("仅末档允许无上限"));
        Assert.Contains(errors, e => e.Contains("末档必须无上限"));
    }

    [Fact]
    public void TieredWithSingleTier_ShouldError()
    {
        var error = SingleError(TieredRule([new PricingTier { UpTo = null, UnitPrice = 4m }]));
        Assert.Contains("单档阶梯无意义", error);
    }

    [Fact]
    public void TieredWithZeroUnitPrice_ShouldError()
    {
        var error = SingleError(TieredRule([
            new PricingTier { UpTo = 1m, UnitPrice = 0m },
            new PricingTier { UpTo = null, UnitPrice = 3m },
        ]));

        Assert.Contains("单价必须 >0", error);
    }

    [Fact]
    public void NegativeSurcharge_ShouldError()
    {
        var rule = Rule();
        var error = SingleError(new PricingRule
        {
            Priority = rule.Priority,
            Name = rule.Name,
            Enabled = true,
            BillingWeightStrategy = BillingWeightStrategy.WeightOnly,
            Conditions = [],
            Freight = rule.Freight,
            Surcharges =
            [
                new PricingSurchargeFeeItem { Name = "装卸费", Basis = PricingSurchargeBasis.PerWaybill, Amount = -5m },
            ],
        });

        Assert.Contains("附加费金额必须 ≥0", error);
    }

    [Fact]
    public void DiscountRateOutOfRange_ShouldError()
    {
        var error = SingleError(DiscountRule(new PricingDiscountFeeItem
        {
            Name = "折扣",
            Mode = PricingDiscountMode.Rate,
            Rate = 100m,
        }));

        Assert.Contains("折扣率必须介于", error);
    }

    [Fact]
    public void RateDiscountWithAmountField_ShouldError()
    {
        var error = SingleError(DiscountRule(new PricingDiscountFeeItem
        {
            Name = "折扣",
            Mode = PricingDiscountMode.Rate,
            Rate = 10m,
            Amount = 5m,
        }));

        Assert.Contains("不支持固定减免金额字段", error);
    }

    [Fact]
    public void AmountDiscountNegative_ShouldError()
    {
        var error = SingleError(DiscountRule(new PricingDiscountFeeItem
        {
            Name = "折扣",
            Mode = PricingDiscountMode.Amount,
            Amount = -1m,
        }));

        Assert.Contains("固定减免金额必须 ≥0", error);
    }

    [Fact]
    public void BlankRuleName_ShouldError()
    {
        var error = SingleError(Rule(name: "  "));
        Assert.Contains("名称不能为空", error);
    }

    private static PricingRule TieredRule(IReadOnlyList<PricingTier> tiers) => new()
    {
        Priority = 1,
        Name = "规则",
        Enabled = true,
        BillingWeightStrategy = BillingWeightStrategy.WeightOnly,
        Conditions = [],
        Freight = new PricingFreightFeeItem { Name = "运费", Type = PricingFreightType.Tiered, Tiers = tiers },
        Surcharges = [],
    };

    private static PricingRule DiscountRule(PricingDiscountFeeItem discount) => new()
    {
        Priority = 1,
        Name = "规则",
        Enabled = true,
        BillingWeightStrategy = BillingWeightStrategy.WeightOnly,
        Conditions = [],
        Freight = new PricingFreightFeeItem { Name = "运费", Type = PricingFreightType.ByWeight, UnitPrice = 1m },
        Surcharges = [],
        Discount = discount,
    };
}
