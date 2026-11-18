using Ltl.Modules.Waybills.Domain;

namespace Ltl.Modules.Waybills.Application;

/// <summary>开单结果：运单标识 + 固化费用 + 计价版本引用。幂等重放返回相同值。</summary>
public sealed record CreateWaybillResult(
    Guid WaybillId,
    string WaybillNo,
    WaybillStatus Status,
    decimal TotalAmount,
    decimal? CodAmount,
    string RuleSetCode,
    int VersionNumber,
    string? QueryCode = null);
