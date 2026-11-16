using Ltl.Modules.Organization.Domain;

namespace Ltl.Modules.Waybills.Domain;

/// <summary>
/// 运单域权限点清单（平台预置，迁移 seed）。Guid 沿 ORG-001 约定（0301.. 运单段），不得变更。
/// </summary>
public static class WaybillPermissionCodes
{
    public const string Module = "waybill";

    public const string WaybillRead = "waybill.read";
    public const string WaybillManage = "waybill.manage";

    public static readonly IReadOnlyList<Permission> All = new List<Permission>
    {
        Permission.Create(WaybillRead, "查看运单（含打印数据）", Module, new Guid("00000000-0000-0000-0000-000000000301")),
        Permission.Create(WaybillManage, "管理运单（开单/修改/确认/取消）", Module, new Guid("00000000-0000-0000-0000-000000000302")),
    };
}
