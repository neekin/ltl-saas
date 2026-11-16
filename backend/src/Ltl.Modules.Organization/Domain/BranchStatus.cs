namespace Ltl.Modules.Organization.Domain;

/// <summary>网点状态：直营营业网点（AGENTS.md §6.2，网点是实际经营单位）。</summary>
public enum BranchStatus
{
    /// <summary>启用。</summary>
    Active = 0,

    /// <summary>停用（禁止承接业务）。</summary>
    Disabled = 1,
}
