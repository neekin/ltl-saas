using Ltl.Modules.Organization.Domain;

namespace Ltl.Modules.Transport.Domain;

/// <summary>运输域权限点清单（平台预置，迁移 seed）。Guid 沿 ORG-001 约定（0401.. 运输段）。</summary>
public static class TransportPermissionCodes
{
    public const string Module = "transport";

    public const string VehicleRead = "transport.vehicle.read";
    public const string VehicleManage = "transport.vehicle.manage";
    public const string TripRead = "transport.trip.read";
    public const string TripManage = "transport.trip.manage";

    public static readonly IReadOnlyList<Permission> All = new List<Permission>
    {
        Permission.Create(VehicleRead, "查看车辆/司机档案", Module, new Guid("00000000-0000-0000-0000-000000000401")),
        Permission.Create(VehicleManage, "管理车辆/司机档案", Module, new Guid("00000000-0000-0000-0000-000000000402")),
        Permission.Create(TripRead, "查看运输批次/配载", Module, new Guid("00000000-0000-0000-0000-000000000403")),
        Permission.Create(TripManage, "管理运输批次/配载/发车/到达", Module, new Guid("00000000-0000-0000-0000-000000000404")),
    };
}
