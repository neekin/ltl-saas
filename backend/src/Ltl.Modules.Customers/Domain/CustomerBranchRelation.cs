using Ltl.SharedKernel;

namespace Ltl.Modules.Customers.Domain;

/// <summary>
/// 客户-网点服务关系（复合主键 (tenant_id, customer_id, branch_id)）。
/// 一个客户可授权多个网点；一个网点可服务多个客户。
/// branch_id 属于 Organization 模块数据：仅数据库层复合外键 (tenant_id, branch_id) → branches(tenant_id, id)，
/// 本模块不引用 Organization 领域实体（AGENTS.md §4.2 模块边界）。
/// </summary>
public sealed class CustomerBranchRelation : ITenantScopedEntity
{
    public Guid TenantId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid BranchId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    private CustomerBranchRelation()
    {
    }

    public static CustomerBranchRelation Create(
        Guid tenantId,
        Guid customerId,
        Guid branchId,
        DateTime nowUtc,
        Guid? createdBy = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("租户 Id 不能为空 Guid。", nameof(tenantId));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("客户 Id 不能为空 Guid。", nameof(customerId));
        }

        if (branchId == Guid.Empty)
        {
            throw new ArgumentException("网点 Id 不能为空 Guid。", nameof(branchId));
        }

        return new CustomerBranchRelation
        {
            TenantId = tenantId,
            CustomerId = customerId,
            BranchId = branchId,
            CreatedAt = EnsureUtc(nowUtc),
            CreatedBy = createdBy,
        };
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        if (value.Kind == DateTimeKind.Local)
        {
            return value.ToUniversalTime();
        }

        throw new ArgumentException("时间必须携带 UTC 或本地时区信息，禁止 Unspecified。", nameof(value));
    }
}
