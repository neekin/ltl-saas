namespace Ltl.Modules.Routing.Domain;

/// <summary>线路/运输方案状态。</summary>
public enum RouteStatus
{
    /// <summary>启用（可被方案引用、可开单）。</summary>
    Active = 0,

    /// <summary>停用。</summary>
    Disabled = 1,
}
