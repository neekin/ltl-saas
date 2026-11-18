using System.Globalization;
using System.Text;

namespace Ltl.Modules.Pricing.Domain;

/// <summary>
/// 计价执行器（纯函数，AGENTS §6.4）：仅依赖（规则定义快照, 输入），同输入同版本结果确定可复现。
/// 匹配语义与计算语义见 PRICE-002 设计文档第 3/4 节（PRICE-001 契约的执行侧）。
/// </summary>
public static class PricingEngine
{
    /// <summary>计费数量与金额的舍入位数（RoundingPolicy.HalfUpTwoDecimals）。</summary>
    private const int RoundDigits = 2;

    /// <summary>
    /// 匹配并计价。无命中规则 → 明确报错（不静默计零）。
    /// </summary>
    /// <exception cref="ArgumentException">输入非法时抛出。</exception>
    /// <exception cref="PricingMatchFailedException">无命中规则时抛出。</exception>
    public static PricingQuoteResult Calculate(PricingRuleDefinition definition, PricingQuoteInput input)
    {
        PricingQuoteInput.Validate(input);

        var rule = MatchRule(definition, input)
            ?? throw new PricingMatchFailedException(
                "没有命中的计价规则：请检查规则集版本内是否存在匹配当前输入（重量/体积/件数/客户/线路/网点）的启用规则。");

        var items = new List<PricingQuoteItem>();
        var freight = CalculateFreight(rule, input, items, out var billingQuantity, out var billingBasis);
        var surchargeTotal = CalculateSurcharges(rule, input, items, out var insuranceFee);

        var subtotal = freight + surchargeTotal;
        var discount = CalculateDiscount(rule, subtotal);
        var total = discount is null ? subtotal : Math.Max(subtotal - discount.Amount, 0m);

        return new PricingQuoteResult
        {
            MatchedRulePriority = rule.Priority,
            MatchedRuleName = rule.Name,
            BillingQuantity = billingQuantity,
            BillingBasis = billingBasis,
            Items = items,
            InsuranceFee = insuranceFee,
            Discount = discount,
            Subtotal = subtotal,
            Total = total,
        };
    }

    private static PricingRule? MatchRule(PricingRuleDefinition definition, PricingQuoteInput input)
    {
        foreach (var rule in definition.Rules.Where(r => r.Enabled).OrderBy(r => r.Priority))
        {
            if (rule.Conditions.All(condition => ConditionHolds(condition, input)))
            {
                return rule;
            }
        }

        return null;
    }

    private static bool ConditionHolds(PricingCondition condition, PricingQuoteInput input)
    {
        // 数值字段
        decimal? numeric = condition.Field switch
        {
            PricingConditionField.WeightKg => input.WeightKg,
            PricingConditionField.VolumeM3 => input.VolumeM3,
            PricingConditionField.PieceCount => input.PieceCount,
            _ => null,
        };

        if (numeric is not null)
        {
            var value = ParseDecimal(condition.Value);
            return condition.Operator switch
            {
                PricingConditionOperator.GreaterThan => numeric > value,
                PricingConditionOperator.GreaterOrEqual => numeric >= value,
                PricingConditionOperator.LessThan => numeric < value,
                PricingConditionOperator.LessOrEqual => numeric <= value,
                PricingConditionOperator.Between => numeric >= value
                    && numeric <= ParseDecimal(condition.Value2!),
                _ => false, // 校验器已拒绝数值字段上的 Equals/In，防御性不命中
            };
        }

        // Id 字段
        Guid? id = condition.Field switch
        {
            PricingConditionField.CustomerId => input.CustomerId,
            PricingConditionField.RouteId => input.RouteId,
            PricingConditionField.OriginBranchId => input.OriginBranchId,
            PricingConditionField.DestBranchId => input.DestBranchId,
            _ => null,
        };

        if (id is null)
        {
            return false; // 输入缺失该 Id → 条件不成立（客户专属规则要求携带客户）
        }

        return condition.Operator switch
        {
            PricingConditionOperator.Equals => id == Guid.ParseExact(condition.Value, "D"),
            PricingConditionOperator.In => condition.Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(v => id == Guid.ParseExact(v, "D")),
            _ => false, // 校验器已拒绝 Id 字段上的区间操作符
        };
    }

    /// <summary>计算运费主项并写入费用明细；输出计费数量与口径（Fixed 运费为 null）。</summary>
    private static decimal CalculateFreight(
        PricingRule rule,
        PricingQuoteInput input,
        List<PricingQuoteItem> items,
        out decimal? billingQuantity,
        out string? billingBasis)
    {
        var freight = rule.Freight;
        decimal freightAmount;
        string detail;

        switch (freight.Type)
        {
            case PricingFreightType.Fixed:
                billingQuantity = null;
                billingBasis = null;
                freightAmount = freight.Amount!.Value;
                detail = $"固定运费 {Format(freightAmount)} 元";
                break;

            case PricingFreightType.ByWeight:
            case PricingFreightType.ByVolume:
            case PricingFreightType.ByPieces:
                (billingQuantity, billingBasis) = ResolveBillingQuantity(rule, input);
                freightAmount = freight.UnitPrice!.Value * billingQuantity.Value;
                detail = $"{billingBasis} × 单价 {Format(freight.UnitPrice.Value)} 元";
                break;

            case PricingFreightType.Tiered:
                (billingQuantity, billingBasis) = ResolveBillingQuantity(rule, input);
                freightAmount = CalculateTiered(freight.Tiers!, billingQuantity.Value, out detail);
                break;

            default:
                throw new PricingMatchFailedException($"未知的运费类型 {freight.Type}。");
        }

        if (freight.MinCharge is { } minCharge && freight.Type != PricingFreightType.Fixed
            && freightAmount < minCharge)
        {
            detail += $"（低于最低收费 {Format(minCharge)}，按最低收费计）";
            freightAmount = minCharge;
        }

        items.AddFreight(freight.Name, Round(freightAmount), detail);
        return Round(freightAmount);
    }

    /// <summary>阶梯计费：逐段 [上一档上界, 本档上界) × 本档单价；末档无上限承接剩余全部。</summary>
    private static decimal CalculateTiered(IReadOnlyList<PricingTier> tiers, decimal quantity, out string detail)
    {
        var builder = new StringBuilder("分段计费");
        var total = 0m;
        var lower = 0m;
        var remaining = quantity;

        foreach (var tier in tiers)
        {
            if (remaining <= 0)
            {
                break;
            }

            var upperText = tier.UpTo is { } bound ? Format(bound) : "∞";
            var segment = tier.UpTo is { } upTo
                ? Math.Min(Math.Max(upTo - lower, 0), remaining)
                : remaining;

            if (segment > 0)
            {
                var segmentAmount = segment * tier.UnitPrice;
                total += segmentAmount;
                builder.Append(
                    $"；[{Format(lower)},{upperText}) {Format(segment)}×{Format(tier.UnitPrice)}={Format(segmentAmount)}");
                remaining -= segment;
            }

            if (tier.UpTo is not null)
            {
                lower = tier.UpTo.Value;
            }
        }

        detail = builder.ToString();
        return total;
    }

    private static decimal CalculateSurcharges(
        PricingRule rule, PricingQuoteInput input, List<PricingQuoteItem> items, out decimal insuranceFee)
    {
        var total = 0m;
        insuranceFee = 0m;

        foreach (var surcharge in rule.Surcharges)
        {
            var amount = surcharge.Basis switch
            {
                PricingSurchargeBasis.PerPiece => surcharge.Amount * input.PieceCount,
                PricingSurchargeBasis.PerDeclaredValue when input.DeclaredValue is { } declared =>
                    declared * surcharge.Rate.GetValueOrDefault() / 100m,
                // 未声明价值时保价费按 0 计（免费保价），明细仍展示口径
                PricingSurchargeBasis.PerDeclaredValue => 0m,
                _ => surcharge.Amount,
            };
            var rounded = Round(amount);
            total += rounded;
            if (surcharge.Basis == PricingSurchargeBasis.PerDeclaredValue)
            {
                insuranceFee = rounded;
            }

            var basisText = surcharge.Basis switch
            {
                PricingSurchargeBasis.PerPiece => $"{Format(surcharge.Amount)} 元/件 × {input.PieceCount} 件",
                PricingSurchargeBasis.PerDeclaredValue => $"声明价值 {Format(input.DeclaredValue ?? 0)} 元 × {Format(surcharge.Rate.GetValueOrDefault())}%",
                _ => $"整票 {Format(surcharge.Amount)} 元",
            };

            items.AddSurcharge(surcharge.Name, rounded, $"{surcharge.Name}：{basisText}");
        }

        return total;
    }

    private static PricingQuoteDiscount? CalculateDiscount(PricingRule rule, decimal subtotal)
    {
        if (rule.Discount is not { } discount)
        {
            return null;
        }

        var amount = discount.Mode == PricingDiscountMode.Rate
            ? subtotal * discount.Rate!.Value / 100m
            : discount.Amount!.Value;
        var rounded = Round(amount);
        var basisText = discount.Mode == PricingDiscountMode.Rate
            ? $"小计 {Format(subtotal)} × {Format(discount.Rate!.Value)}%"
            : $"固定减免 {Format(discount.Amount!.Value)}";

        return new PricingQuoteDiscount
        {
            Name = discount.Name,
            Amount = rounded,
            Detail = $"{discount.Name}：{basisText} = {Format(rounded)}",
        };
    }

    /// <summary>按规则的计费重量策略解析计费数量（2 位 HalfUp），并返回口径解释。</summary>
    private static (decimal Quantity, string Basis) ResolveBillingQuantity(
        PricingRule rule, PricingQuoteInput input)
    {
        switch (rule.BillingWeightStrategy)
        {
            case BillingWeightStrategy.WeightOnly:
                return (Round(input.WeightKg), $"计费重量 {Format(Round(input.WeightKg))} kg");

            case BillingWeightStrategy.VolumeOnly:
                return (Round(input.VolumeM3), $"计费体积 {Format(Round(input.VolumeM3))} m³");

            case BillingWeightStrategy.PieceCount:
                return (input.PieceCount, $"计费件数 {input.PieceCount} 件");

            case BillingWeightStrategy.WeightVolumeMax:
            case BillingWeightStrategy.WeightVolumeMin:
                var volumeWeight = input.VolumeM3 * rule.VolumeToWeightFactor!.Value;
                var takeMax = rule.BillingWeightStrategy == BillingWeightStrategy.WeightVolumeMax;
                var useWeight = takeMax ? input.WeightKg >= volumeWeight : input.WeightKg <= volumeWeight;
                var comparison = $"（{(takeMax ? "取大" : "取小")}：重量 {Format(input.WeightKg)} vs 体积重 {Format(volumeWeight)}）";

                return useWeight
                    ? (Round(input.WeightKg), $"计费重量 {Format(Round(input.WeightKg))} kg{comparison}")
                    : (Round(volumeWeight), $"计费体积重 {Format(Round(volumeWeight))} kg{comparison}");

            default:
                throw new PricingMatchFailedException($"未知的计费重量策略 {rule.BillingWeightStrategy}。");
        }
    }

    private static void AddFreight(this List<PricingQuoteItem> items, string name, decimal amount, string detail) =>
        items.Add(new PricingQuoteItem
        {
            Kind = PricingFeeKind.Freight,
            Name = name,
            Amount = amount,
            Detail = detail,
        });

    private static void AddSurcharge(this List<PricingQuoteItem> items, string name, decimal amount, string detail) =>
        items.Add(new PricingQuoteItem
        {
            Kind = PricingFeeKind.Surcharge,
            Name = name,
            Amount = amount,
            Detail = detail,
        });

    private static decimal Round(decimal value) =>
        Math.Round(value, RoundDigits, MidpointRounding.AwayFromZero);

    private static string Format(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    private static decimal ParseDecimal(string text) =>
        decimal.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture);
}
