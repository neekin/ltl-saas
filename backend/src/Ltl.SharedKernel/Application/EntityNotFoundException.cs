namespace Ltl.SharedKernel.Application;

/// <summary>
/// 目标实体不存在或当前上下文不可访问（跨租户实体因全局查询过滤器同样查不到）。
/// 后续 API 任务统一映射为 404 ProblemDetails。
/// </summary>
public sealed class EntityNotFoundException(string entityType, object key)
    : Exception($"{entityType}（{key}）不存在或不可访问。");
