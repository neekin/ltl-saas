namespace Ltl.Modules.Routing.Domain;

/// <summary>
/// 线路站点。sequence：0=起点，连续递增，最后一个=终点；
/// 同一线路内同一网点不得出现两次（ux_route_stops_tenant_route_branch）。
/// </summary>
public sealed class RouteStop : TenantScopedEntity
{
    public Guid RouteId { get; private set; }

    public Guid BranchId { get; private set; }

    public int Sequence { get; private set; }

    public string? Remark { get; private set; }

    private RouteStop()
    {
    }

    /// <exception cref="ArgumentException">tenantId/routeId/branchId 为空或 sequence 为负时抛出。</exception>
    public static RouteStop Create(
        Guid tenantId,
        Guid routeId,
        Guid branchId,
        int sequence,
        DateTime nowUtc,
        string? remark = null,
        Guid? createdBy = null)
    {
        if (routeId == Guid.Empty)
        {
            throw new ArgumentException("线路 Id 不能为空 Guid。", nameof(routeId));
        }

        if (branchId == Guid.Empty)
        {
            throw new ArgumentException("网点 Id 不能为空 Guid。", nameof(branchId));
        }

        if (sequence < 0)
        {
            throw new ArgumentException("站点顺序必须从 0 开始。", nameof(sequence));
        }

        return new RouteStop
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            RouteId = routeId,
            BranchId = branchId,
            Sequence = sequence,
            Remark = NormalizeOptional(remark, 200),
            CreatedAt = EnsureUtc(nowUtc),
            CreatedBy = createdBy,
        };
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"备注长度不能超过 {maxLength}。", nameof(value));
        }

        return trimmed;
    }
}
