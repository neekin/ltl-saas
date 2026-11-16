namespace Ltl.Modules.Routing.Domain;

/// <summary>
/// 线路站点顺序完整性校验（纯函数，ROUTE-001 设计文档第 4 节）。
/// 站点集合变更时必须整体重设并通过校验；数据层约束只保证同序号/同网点唯一，
/// 连续性与首尾匹配由本校验器在变更时强制执行。
/// </summary>
public static class RouteSpec
{
    public sealed record StopSpec(Guid BranchId, int Sequence);

    public static IReadOnlyList<string> Validate(
        Guid originBranchId,
        Guid destinationBranchId,
        IReadOnlyCollection<StopSpec> stops)
    {
        var errors = new List<string>();

        if (stops.Count < 2)
        {
            errors.Add("线路至少需要起点和终点两个站点。");
            return errors;
        }

        var ordered = stops.OrderBy(s => s.Sequence).ToArray();

        // sequence 必须为 0..N-1 连续
        for (var i = 0; i < ordered.Length; i++)
        {
            if (ordered[i].Sequence != i)
            {
                errors.Add($"站点顺序必须为 0 到 {ordered.Length - 1} 连续（发现 {ordered[i].Sequence}）。");
                break;
            }
        }

        // 同一线路同一网点不得出现两次
        var duplicate = ordered
            .GroupBy(s => s.BranchId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();
        if (duplicate.Length > 0)
        {
            errors.Add("同一线路中同一网点不得出现多次。");
        }

        // 首站=起点、末站=终点
        if (ordered[0].BranchId != originBranchId)
        {
            errors.Add("第一个站点必须是线路起点网点。");
        }

        if (ordered[^1].BranchId != destinationBranchId)
        {
            errors.Add("最后一个站点必须是线路终点网点。");
        }

        return errors;
    }
}
