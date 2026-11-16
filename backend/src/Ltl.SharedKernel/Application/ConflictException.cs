namespace Ltl.SharedKernel.Application;

/// <summary>
/// 资源冲突（如唯一编码重复、状态冲突）。API 任务统一映射为 409 ProblemDetails。
/// </summary>
public sealed class ConflictException(string message) : Exception(message);
