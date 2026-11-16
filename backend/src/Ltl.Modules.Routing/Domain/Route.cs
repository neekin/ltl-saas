namespace Ltl.Modules.Routing.Domain;

/// <summary>
/// 业务线路定义（AGENTS.md §6.2：Route 是线路定义，不是实际车辆运输）。
/// 起点 ≠ 终点；站点序列见 RouteStop（0=起点，连续递增）。
/// </summary>
public sealed class Route : TenantScopedEntity
{
    /// <summary>线路编码，租户内唯一（ux_routes_tenant_code）。</summary>
    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public Guid OriginBranchId { get; private set; }

    public Guid DestinationBranchId { get; private set; }

    public RouteStatus Status { get; private set; }

    private Route()
    {
    }

    /// <exception cref="ArgumentException">tenantId 为空、code/name 非法或起点等于终点时抛出。</exception>
    public static Route Create(
        Guid tenantId,
        string code,
        string name,
        Guid originBranchId,
        Guid destinationBranchId,
        DateTime nowUtc,
        Guid? createdBy = null)
    {
        if (originBranchId == Guid.Empty || destinationBranchId == Guid.Empty)
        {
            throw new ArgumentException("起点/终点网点 Id 不能为空 Guid。");
        }

        if (originBranchId == destinationBranchId)
        {
            throw new ArgumentException("线路起点与终点不能是同一网点。");
        }

        return new Route
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            Code = RequireNonBlank(code, "线路编码", 64),
            Name = RequireNonBlank(name, "线路名称", 200),
            OriginBranchId = originBranchId,
            DestinationBranchId = destinationBranchId,
            Status = RouteStatus.Active,
            CreatedAt = EnsureUtc(nowUtc),
            CreatedBy = createdBy,
        };
    }

    /// <summary>停用线路（幂等）。</summary>
    public void Disable(DateTime nowUtc, Guid? updatedBy = null)
    {
        if (Status == RouteStatus.Disabled)
        {
            return;
        }

        Status = RouteStatus.Disabled;
        Touch(nowUtc, updatedBy);
    }

    /// <summary>启用线路（幂等）。</summary>
    public void Enable(DateTime nowUtc, Guid? updatedBy = null)
    {
        if (Status == RouteStatus.Active)
        {
            return;
        }

        Status = RouteStatus.Active;
        Touch(nowUtc, updatedBy);
    }
}
