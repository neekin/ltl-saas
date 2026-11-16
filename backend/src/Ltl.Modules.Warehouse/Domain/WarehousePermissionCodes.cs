using Ltl.Modules.Organization.Domain;

namespace Ltl.Modules.Warehouse.Domain;

/// <summary>
/// 仓储域权限点清单（平台预置，迁移 seed）。Guid 沿 ORG-001 约定（0201.. 仓储段），不得变更。
/// </summary>
public static class WarehousePermissionCodes
{
    public const string Module = "warehouse";

    public const string CargoReceive = "warehouse.cargo.receive";
    public const string CargoRead = "warehouse.cargo.read";
    public const string CargoSort = "warehouse.cargo.sort";

    public static readonly IReadOnlyList<Permission> All = new List<Permission>
    {
        Permission.Create(CargoReceive, "收货登记（称重、体积复核）", Module, new Guid("00000000-0000-0000-0000-000000000201")),
        Permission.Create(CargoRead, "查看收货记录与仓储状态", Module, new Guid("00000000-0000-0000-0000-000000000202")),
        Permission.Create(CargoSort, "分拣任务操作", Module, new Guid("00000000-0000-0000-0000-000000000203")),
    };
}
