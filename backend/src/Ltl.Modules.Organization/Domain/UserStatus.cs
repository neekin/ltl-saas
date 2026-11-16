namespace Ltl.Modules.Organization.Domain;

public enum UserStatus
{
    /// <summary>启用。</summary>
    Active = 0,

    /// <summary>禁用（登录与权限即时失效，P1 验收标准）。</summary>
    Disabled = 1,
}
