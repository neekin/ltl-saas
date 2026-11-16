using Ltl.SharedKernel.Application;

namespace Ltl.Modules.Customers.Application;

/// <summary>
/// 客户-网点服务关系维护。幂等授权/收回与双向查询（P2 验收：一个客户可授权多个网点、一个网点可服务多个客户）。
/// 所有操作实时查询（当前租户上下文经 CustomersDbContext 过滤器限定）。
/// </summary>
public interface ICustomerRelationshipService
{
    /// <summary>授权客户由指定网点服务（幂等；客户与网点必须在当前租户内）。</summary>
    Task GrantBranchServiceAsync(Guid customerId, Guid branchId, Guid? operatedBy = null);

    /// <summary>收回客户的网点服务授权（幂等：不存在时无操作）。</summary>
    Task RevokeBranchServiceAsync(Guid customerId, Guid branchId);

    /// <summary>客户被授权服务的网点 Id 集合。</summary>
    Task<IReadOnlyCollection<Guid>> GetServicedBranchIdsAsync(Guid customerId);

    /// <summary>授权由指定网点服务的客户 Id 集合。</summary>
    Task<IReadOnlyCollection<Guid>> GetCustomersServedByBranchAsync(Guid branchId);
}
