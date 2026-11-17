using Ltl.Modules.Organization.Domain;

namespace Ltl.Modules.Arrivals.Domain;

/// <summary>到货域权限点清单（平台预置，迁移 seed）。Guid 沿 ORG-001 约定（0801.. 到货段）。</summary>
public static class ArrivalPermissionCodes
{
    public const string Module = "arrival";

    public const string ArrivalRead = "arrival.read";
    public const string ArrivalRecord = "arrival.record";

    public static readonly IReadOnlyList<Permission> All = new List<Permission>
    {
        Permission.Create(ArrivalRead, "查看到货记录", Module, new Guid("00000000-0000-0000-0000-000000000801")),
        Permission.Create(ArrivalRecord, "到货登记", Module, new Guid("00000000-0000-0000-0000-000000000802")),
    };
}
