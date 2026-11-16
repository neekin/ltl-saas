namespace Ltl.Modules.Routing.Domain;

/// <summary>
/// 运输方案：基于线路的计划运输路径，可由多个计划区段组成（1 区段=直达，多区段=中转）。
/// 实际车辆运输（TransportTrip）属 Transport 模块，P5 实现。
/// </summary>
public sealed class TransportPlan : TenantScopedEntity
{
    public Guid RouteId { get; private set; }

    public string Name { get; private set; } = null!;

    public RouteStatus Status { get; private set; }

    private TransportPlan()
    {
    }

    /// <exception cref="ArgumentException">tenantId/routeId 为空或 name 非法时抛出。</exception>
    public static TransportPlan Create(
        Guid tenantId,
        Guid routeId,
        string name,
        DateTime nowUtc,
        Guid? createdBy = null)
    {
        if (routeId == Guid.Empty)
        {
            throw new ArgumentException("线路 Id 不能为空 Guid。", nameof(routeId));
        }

        return new TransportPlan
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            RouteId = routeId,
            Name = RequireNonBlank(name, "方案名称", 200),
            Status = RouteStatus.Active,
            CreatedAt = EnsureUtc(nowUtc),
            CreatedBy = createdBy,
        };
    }

    /// <summary>停用方案（幂等）。</summary>
    public void Disable(DateTime nowUtc, Guid? updatedBy = null)
    {
        if (Status == RouteStatus.Disabled)
        {
            return;
        }

        Status = RouteStatus.Disabled;
        Touch(nowUtc, updatedBy);
    }

    /// <summary>启用方案（幂等）。</summary>
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
