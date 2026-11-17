using Ltl.Modules.Organization.Domain;

namespace Ltl.Modules.Delivery.Domain;

/// <summary>交付域权限点清单。Guid 沿 ORG-001 约定（0901.. 交付段）。</summary>
public static class DeliveryPermissionCodes
{
    public const string Module = "delivery";
    public const string DeliveryRead = "delivery.read";
    public const string DeliveryManage = "delivery.manage";

    public static readonly IReadOnlyList<Permission> All = new List<Permission>
    {
        Permission.Create(DeliveryRead, "查看交付单/凭证", Module, new Guid("00000000-0000-0000-0000-000000000901")),
        Permission.Create(DeliveryManage, "管理交付/签收/拒收", Module, new Guid("00000000-0000-0000-0000-000000000902")),
    };
}
