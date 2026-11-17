using Ltl.Modules.Organization.Domain;

namespace Ltl.Modules.Finance.Domain;

/// <summary>财务域权限点清单。</summary>
public static class FinancePermissionCodes
{
    public const string Module = "finance";
    public const string FinanceRead = "finance.read";
    public const string FinanceManage = "finance.manage";

    public static readonly IReadOnlyList<Permission> All = new List<Permission>
    {
        Permission.Create(FinanceRead, "查看应收/收款/退款", Module, new Guid("00000000-0000-0000-0000-000000001001")),
        Permission.Create(FinanceManage, "管理收款/退款/核销", Module, new Guid("00000000-0000-0000-0000-000000001002")),
    };
}
