namespace Ltl.Modules.Routing.Domain;

/// <summary>
/// 运输方案区段完整性校验（纯函数，ROUTE-001 设计文档第 4 节）：
/// 区段 1..N 连续、首尾相连、端点落在线路站点序列内、首区段起点=线路起点、尾区段终点=线路终点。
/// </summary>
public static class TransportPlanSpec
{
    public sealed record SegmentSpec(Guid FromBranchId, Guid ToBranchId, int Sequence);

    public static IReadOnlyList<string> Validate(
        Guid originBranchId,
        Guid destinationBranchId,
        IReadOnlyCollection<Guid> routeStopBranchIds,
        IReadOnlyCollection<SegmentSpec> segments)
    {
        var errors = new List<string>();

        if (segments.Count < 1)
        {
            errors.Add("运输方案至少需要一个区段（1 区段=直达）。");
            return errors;
        }

        var ordered = segments.OrderBy(s => s.Sequence).ToArray();
        var stopBranchIds = routeStopBranchIds.ToHashSet();

        // sequence 必须为 1..N 连续
        for (var i = 0; i < ordered.Length; i++)
        {
            if (ordered[i].Sequence != i + 1)
            {
                errors.Add($"区段顺序必须为 1 到 {ordered.Length} 连续（发现 {ordered[i].Sequence}）。");
                break;
            }
        }

        foreach (var segment in ordered)
        {
            if (segment.FromBranchId == segment.ToBranchId)
            {
                errors.Add($"区段 {segment.Sequence} 的起点与终点不能是同一网点。");
            }

            // 区段端点必须落在线路站点序列内
            if (!stopBranchIds.Contains(segment.FromBranchId))
            {
                errors.Add($"区段 {segment.Sequence} 的起点网点不在线路站点中。");
            }

            if (!stopBranchIds.Contains(segment.ToBranchId))
            {
                errors.Add($"区段 {segment.Sequence} 的终点网点不在线路站点中。");
            }
        }

        // 首区段起点 = 线路起点
        if (ordered[0].FromBranchId != originBranchId)
        {
            errors.Add("第一个区段的起点必须是线路起点网点。");
        }

        // 尾区段终点 = 线路终点
        if (ordered[^1].ToBranchId != destinationBranchId)
        {
            errors.Add("最后一个区段的终点必须是线路终点网点。");
        }

        // 相邻区段首尾相连
        for (var i = 0; i < ordered.Length - 1; i++)
        {
            if (ordered[i].ToBranchId != ordered[i + 1].FromBranchId)
            {
                errors.Add($"区段 {ordered[i].Sequence} 的终点必须与区段 {ordered[i + 1].Sequence} 的起点相连。");
            }
        }

        return errors;
    }
}
