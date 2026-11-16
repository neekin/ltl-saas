namespace Ltl.Modules.Customers.Migrations;

/// <summary>权限点 seed 数据（CUS-002，供迁移引用）。</summary>
public static class CustomerPermissionSeed
{
    public static readonly (Guid Id, string Code, string Name, string Module)[] Rows =
    [
        (new Guid("00000000-0000-0000-0000-000000000501"), Domain.CustomerPermissionCodes.CustomerRead, "查看客户档案", Domain.CustomerPermissionCodes.Module),
        (new Guid("00000000-0000-0000-0000-000000000502"), Domain.CustomerPermissionCodes.CustomerManage, "管理客户（档案/联系人/地址/网点授权）", Domain.CustomerPermissionCodes.Module),
    ];
}
