namespace Ltl.Modules.Tenancy.Domain;

/// <summary>租户状态。</summary>
public enum TenantStatus
{
    /// <summary>启用（正常经营）。</summary>
    Active = 0,

    /// <summary>停用（禁止登录与业务操作）。</summary>
    Disabled = 1,
}
