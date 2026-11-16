using Ltl.Modules.Organization.Domain;

namespace Ltl.Modules.Customers.Domain;

/// <summary>客户域权限点清单（平台预置，迁移 seed）。Guid 沿 ORG-001 约定（0501.. 客户段）。</summary>
public static class CustomerPermissionCodes
{
    public const string Module = "customer";

    public const string CustomerRead = "customer.read";
    public const string CustomerManage = "customer.manage";

    public static readonly IReadOnlyList<Permission> All = new List<Permission>
    {
        Permission.Create(CustomerRead, "查看客户档案", Module, new Guid("00000000-0000-0000-0000-000000000501")),
        Permission.Create(CustomerManage, "管理客户（档案/联系人/地址/网点授权）", Module, new Guid("00000000-0000-0000-0000-000000000502")),
    };
}
