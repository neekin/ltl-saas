namespace Ltl.Modules.Warehouse.Domain;

/// <summary>
/// 仓储作业领域违规（状态守卫、任务生命周期）。API 映射为 409。
/// </summary>
public sealed class WarehouseDomainException(string message) : Exception(message);
