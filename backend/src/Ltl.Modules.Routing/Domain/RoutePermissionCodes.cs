using Ltl.Modules.Organization.Domain;

namespace Ltl.Modules.Routing.Domain;

/// <summary>线路域权限点清单（平台预置，迁移 seed）。Guid 沿 ORG-001 约定（0601.. 线路段）。</summary>
public static class RoutePermissionCodes
{
    public const string Module = "routing";

    public const string RouteRead = "route.read";
    public const string RouteManage = "route.manage";

    public static readonly IReadOnlyList<Permission> All = new List<Permission>
    {
        Permission.Create(RouteRead, "查看线路/运输方案", Module, new Guid("00000000-0000-0000-0000-000000000601")),
        Permission.Create(RouteManage, "管理线路/站点/运输方案", Module, new Guid("00000000-0000-0000-0000-000000000602")),
    };
}
