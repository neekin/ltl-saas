namespace Ltl.Modules.Waybills.Domain;

/// <summary>
/// 运单领域违规（状态机守卫、Draft 边界等）。后续 API 任务映射为 409/422 类错误。
/// </summary>
public sealed class WaybillDomainException(string message) : Exception(message);
