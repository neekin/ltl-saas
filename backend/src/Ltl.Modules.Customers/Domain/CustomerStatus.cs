namespace Ltl.Modules.Customers.Domain;

/// <summary>客户状态。</summary>
public enum CustomerStatus
{
    /// <summary>启用（可正常开单）。</summary>
    Active = 0,

    /// <summary>停用（不可新开业务；历史数据保留）。</summary>
    Disabled = 1,
}
