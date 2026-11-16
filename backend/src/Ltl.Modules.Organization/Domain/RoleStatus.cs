namespace Ltl.Modules.Organization.Domain;

public enum RoleStatus
{
    /// <summary>启用。</summary>
    Active = 0,

    /// <summary>停用（停用后角色权限即时失效，P1 验收标准）。</summary>
    Disabled = 1,
}
