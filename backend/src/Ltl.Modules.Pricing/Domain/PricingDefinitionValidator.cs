using System.Globalization;

namespace Ltl.Modules.Pricing.Domain;

/// <summary>
/// 规则定义语义校验器（纯函数，AGENTS §6.4：结构校验由反序列化的强类型模型 + 序列化选项保证，
/// 本校验器负责语义）。返回全部错误（非首条）；零错误方可入库/发布。
/// </summary>
public static class PricingDefinitionValidator
{
    public static IReadOnlyList<string> Validate(PricingRuleDefinition definition)
    {
        var errors = new List<string>();

        if (definition.Rounding != RoundingPolicy.HalfUpTwoDecimals)
        {
            errors.Add($"不支持的舍入策略 {definition.Rounding}（当前仅允许 HalfUpTwoDecimals）。");
        }

        if (definition.Rules.Count == 0)
        {
            errors.Add("规则列表不能为空（至少 1 条规则）。");
        }

        var priorities = new HashSet<int>();

        foreach (var rule in definition.Rules)
        {
            ValidateRule(rule, priorities, errors);
        }

        return errors;
    }

    private static void ValidateRule(PricingRule rule, HashSet<int> priorities, List<string> errors)
    {
        var label = $"规则[{rule.Priority}]";

        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            errors.Add($"{label}：名称不能为空。");
        }
        else if (rule.Name.Trim().Length > 200)
        {
            errors.Add($"{label}：名称长度不能超过 200。");
        }

        if (rule.Priority < 1)
        {
            errors.Add($"{label}：优先级必须 ≥1。");
        }
        else if (!priorities.Add(rule.Priority))
        {
            errors.Add($"{label}：优先级在集合内重复。");
        }

        switch (rule.BillingWeightStrategy)
        {
            case BillingWeightStrategy.WeightVolumeMax:
            case BillingWeightStrategy.WeightVolumeMin:
                if (rule.VolumeToWeightFactor is not { } factor || factor <= 0)
                {
                    errors.Add($"{label}：策略 {rule.BillingWeightStrategy} 必须提供体积重系数且 >0。");
                }

                break;
            case BillingWeightStrategy.WeightOnly:
            case BillingWeightStrategy.VolumeOnly:
            case BillingWeightStrategy.PieceCount:
                if (rule.VolumeToWeightFactor is not null)
                {
                    errors.Add($"{label}：策略 {rule.BillingWeightStrategy} 不使用体积重系数，请移除该字段。");
                }

                break;
            default:
                errors.Add($"{label}：未知的计费重量策略。");
                break;
        }

        for (var i = 0; i < rule.Conditions.Count; i++)
        {
            ValidateCondition(rule.Conditions[i], $"{label} 条件#{i + 1}", errors);
        }

        ValidateFreight(rule, label, errors);

        for (var i = 0; i < rule.Surcharges.Count; i++)
        {
            ValidateSurcharge(rule.Surcharges[i], $"{label} 附加费#{i + 1}", errors);
        }

        if (rule.Discount is { } discount)
        {
            ValidateDiscount(discount, label, errors);
        }
    }

    private static void ValidateCondition(
        PricingCondition condition,
        string label,
        List<string> errors)
    {
        var isNumericField = condition.Field
            is PricingConditionField.WeightKg
            or PricingConditionField.VolumeM3
            or PricingConditionField.PieceCount;
        var isIdField = condition.Field
            is PricingConditionField.CustomerId
            or PricingConditionField.RouteId
            or PricingConditionField.OriginBranchId
            or PricingConditionField.DestBranchId;

        if (!isNumericField && !isIdField)
        {
            errors.Add($"{label}：未知的条件字段。");
            return;
        }

        switch (condition.Operator)
        {
            case PricingConditionOperator.Equals:
            case PricingConditionOperator.In:
                if (isNumericField)
                {
                    errors.Add($"{label}：数值字段（重量/体积/件数）不支持 Equals/In，请使用比较操作符或 Between。");
                    return;
                }

                break;

            case PricingConditionOperator.GreaterThan:
            case PricingConditionOperator.GreaterOrEqual:
            case PricingConditionOperator.LessThan:
            case PricingConditionOperator.LessOrEqual:
            case PricingConditionOperator.Between:
                if (isIdField)
                {
                    errors.Add($"{label}：Id 字段（客户/线路/网点）仅支持 Equals/In。");
                    return;
                }

                break;

            default:
                errors.Add($"{label}：未知的条件操作符。");
                return;
        }

        if (isNumericField)
        {
            ValidateNumericValue(condition, label, errors);
            return;
        }

        // Id 字段：Equals 单个 Guid；In 逗号分隔多个 Guid
        var idValues = condition.Operator == PricingConditionOperator.In
            ? condition.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [condition.Value];

        if (idValues.Length == 0)
        {
            errors.Add($"{label}：值不能为空。");
            return;
        }

        foreach (var idValue in idValues)
        {
            if (!Guid.TryParseExact(idValue, "D", out _))
            {
                errors.Add($"{label}：值“{idValue}”不是合法的 Guid。");
            }
        }
    }

    private static void ValidateNumericValue(PricingCondition condition, string label, List<string> errors)
    {
        if (!TryParseNonNegativeDecimal(condition.Value, out var value))
        {
            errors.Add($"{label}：值“{condition.Value}”不是合法的非负数。");
            return;
        }

        if (condition.Field == PricingConditionField.PieceCount
            && value != Math.Floor(value))
        {
            errors.Add($"{label}：件数必须为整数。");
            return;
        }

        if (condition.Operator == PricingConditionOperator.Between)
        {
            if (condition.Value2 is null
                || !TryParseNonNegativeDecimal(condition.Value2, out var value2))
            {
                errors.Add($"{label}：Between 必须提供合法的非负上界值。");
                return;
            }

            if (value >= value2)
            {
                errors.Add($"{label}：Between 区间下界必须小于上界（{value} vs {value2}）。");
            }
        }
        else if (condition.Value2 is not null)
        {
            errors.Add($"{label}：仅 Between 使用上界值，请移除 Value2。");
        }
    }

    private static void ValidateFreight(PricingRule rule, string label, List<string> errors)
    {
        var freight = rule.Freight;

        if (string.IsNullOrWhiteSpace(freight.Name) || freight.Name.Trim().Length > 100)
        {
            errors.Add($"{label}：运费项名称不能为空且长度 ≤100。");
        }

        switch (freight.Type)
        {
            case PricingFreightType.Fixed:
                if (freight.Amount is not { } amount || amount <= 0)
                {
                    errors.Add($"{label}：固定运费金额必须 >0（免运费促销用折扣表达）。");
                }

                if (freight.UnitPrice is not null || freight.Tiers is not null)
                {
                    errors.Add($"{label}：固定运费不支持单价/阶梯字段。");
                }

                break;

            case PricingFreightType.ByWeight:
            case PricingFreightType.ByVolume:
            case PricingFreightType.ByPieces:
                if (freight.UnitPrice is not { } unitPrice || unitPrice <= 0)
                {
                    errors.Add($"{label}：{freight.Type} 运费单价必须 >0。");
                }

                if (freight.Amount is not null || freight.Tiers is not null)
                {
                    errors.Add($"{label}：{freight.Type} 不支持固定金额/阶梯字段。");
                }

                break;

            case PricingFreightType.Tiered:
                ValidateTiers(freight.Tiers, label, errors);

                if (freight.Amount is not null || freight.UnitPrice is not null)
                {
                    errors.Add($"{label}：阶梯运费不支持固定金额/单价字段（单价在每档上）。");
                }

                break;

            default:
                errors.Add($"{label}：未知的运费类型。");
                break;
        }

        if (freight.MinCharge is { } minCharge && minCharge < 0)
        {
            errors.Add($"{label}：最低收费必须 ≥0。");
        }

        if (freight.Type == PricingFreightType.Fixed && freight.MinCharge is not null)
        {
            errors.Add($"{label}：固定运费不支持最低收费。");
        }
    }

    private static void ValidateTiers(IReadOnlyList<PricingTier>? tiers, string label, List<string> errors)
    {
        if (tiers is null || tiers.Count == 0)
        {
            errors.Add($"{label}：阶梯运费必须提供至少 1 档。");
            return;
        }

        decimal? previous = null;

        for (var i = 0; i < tiers.Count; i++)
        {
            var tier = tiers[i];
            var tierLabel = $"{label} 阶梯#{i + 1}";

            if (tier.UnitPrice <= 0)
            {
                errors.Add($"{tierLabel}：单价必须 >0。");
            }

            if (tier.UpTo is { } upTo)
            {
                if (upTo <= 0)
                {
                    errors.Add($"{tierLabel}：上界必须 >0。");
                }
                else if (previous is { } prev && upTo <= prev)
                {
                    errors.Add($"{tierLabel}：上界必须严格大于上一档上界（{upTo} vs {prev}）。");
                }

                previous = upTo;
            }
            else if (i < tiers.Count - 1)
            {
                errors.Add($"{tierLabel}：仅末档允许无上限（null）。");
            }
            else if (tiers.Count == 1)
            {
                errors.Add($"{tierLabel}：单档阶梯无意义，请使用按重/按体积/按件计费。");
            }
        }

        if (tiers[^1].UpTo is not null)
        {
            errors.Add($"{label}：末档必须无上限（UpTo=null，超出部分按末档单价计费）。");
        }
    }

    private static void ValidateSurcharge(PricingSurchargeFeeItem surcharge, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(surcharge.Name) || surcharge.Name.Trim().Length > 100)
        {
            errors.Add($"{label}：名称不能为空且长度 ≤100。");
        }

        if (surcharge.Amount < 0)
        {
            errors.Add($"{label}：附加费金额必须 ≥0。");
        }

        // 保价费（按声明价值费率）：Rate 必填且 ≥0；其他基准不得携带 Rate
        if (surcharge.Basis == PricingSurchargeBasis.PerDeclaredValue)
        {
            if (surcharge.Rate is null)
            {
                errors.Add($"{label}：按声明价值费率的附加费必须填写费率（Rate，%）。");
            }
            else if (surcharge.Rate < 0)
            {
                errors.Add($"{label}：保价费率必须 ≥0。");
            }
        }
        else if (surcharge.Rate is not null)
        {
            errors.Add($"{label}：仅按声明价值费率（保价费）的附加费可填写费率（Rate）。");
        }
    }

    private static void ValidateDiscount(PricingDiscountFeeItem discount, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(discount.Name) || discount.Name.Trim().Length > 100)
        {
            errors.Add($"{label}：折扣项名称不能为空且长度 ≤100。");
        }

        switch (discount.Mode)
        {
            case PricingDiscountMode.Rate:
                if (discount.Rate is not { } rate || rate <= 0 || rate >= 100)
                {
                    errors.Add($"{label}：折扣率必须介于 0 与 100 之间（不含端点；全免用固定减免表达）。");
                }

                if (discount.Amount is not null)
                {
                    errors.Add($"{label}：比例折扣不支持固定减免金额字段。");
                }

                break;

            case PricingDiscountMode.Amount:
                if (discount.Amount is not { } amount || amount < 0)
                {
                    errors.Add($"{label}：固定减免金额必须 ≥0。");
                }

                if (discount.Rate is not null)
                {
                    errors.Add($"{label}：固定减免不支持折扣率字段。");
                }

                break;

            default:
                errors.Add($"{label}：未知的折扣方式。");
                break;
        }
    }

    private static bool TryParseNonNegativeDecimal(string? text, out decimal value)
    {
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value)
            && value >= 0;
    }
}
