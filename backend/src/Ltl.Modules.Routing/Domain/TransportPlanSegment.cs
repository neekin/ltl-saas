namespace Ltl.Modules.Routing.Domain;

/// <summary>
/// 计划运输区段。sequence：1 开始连续递增；区段必须首尾相连且端点落在线路站点序列内
/// （TransportPlanSpec.Validate，ROUTE-001 设计文档第 4 节）。
/// </summary>
public sealed class TransportPlanSegment : TenantScopedEntity
{
    public Guid PlanId { get; private set; }

    public int Sequence { get; private set; }

    public Guid FromBranchId { get; private set; }

    public Guid ToBranchId { get; private set; }

    private TransportPlanSegment()
    {
    }

    /// <exception cref="ArgumentException">Id 为空、sequence 非正或起点等于终点时抛出。</exception>
    public static TransportPlanSegment Create(
        Guid tenantId,
        Guid planId,
        int sequence,
        Guid fromBranchId,
        Guid toBranchId,
        DateTime nowUtc,
        Guid? createdBy = null)
    {
        if (planId == Guid.Empty)
        {
            throw new ArgumentException("方案 Id 不能为空 Guid。", nameof(planId));
        }

        if (sequence < 1)
        {
            throw new ArgumentException("区段顺序必须从 1 开始。", nameof(sequence));
        }

        if (fromBranchId == Guid.Empty || toBranchId == Guid.Empty)
        {
            throw new ArgumentException("区段起点/终点网点 Id 不能为空 Guid。");
        }

        if (fromBranchId == toBranchId)
        {
            throw new ArgumentException("区段起点与终点不能是同一网点（空区段无意义）。");
        }

        return new TransportPlanSegment
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            PlanId = planId,
            Sequence = sequence,
            FromBranchId = fromBranchId,
            ToBranchId = toBranchId,
            CreatedAt = EnsureUtc(nowUtc),
            CreatedBy = createdBy,
        };
    }
}
