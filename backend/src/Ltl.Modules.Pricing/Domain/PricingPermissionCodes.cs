using Ltl.Modules.Organization.Domain;

namespace Ltl.Modules.Pricing.Domain;

/// <summary>计费域权限点清单（平台预置，迁移 seed）。Guid 沿 ORG-001 约定（0701.. 计费段）。</summary>
public static class PricingPermissionCodes
{
    public const string Module = "pricing";

    public const string PricingRead = "pricing.read";
    public const string PricingManage = "pricing.manage";

    public static readonly IReadOnlyList<Permission> All = new List<Permission>
    {
        Permission.Create(PricingRead, "查看计价规则/试算", Module, new Guid("00000000-0000-0000-0000-000000000701")),
        Permission.Create(PricingManage, "管理计价规则（版本/发布）", Module, new Guid("00000000-0000-0000-0000-000000000702")),
    };
}
