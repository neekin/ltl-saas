namespace Ltl.Modules.Organization.Application.Authorization;

/// <summary>
/// 已认证但无所需权限点（实时校验失败）。API 任务映射为 403 ProblemDetails；
/// 与 401（未认证）和 404（跨租户不可见）语义分离。
/// </summary>
public sealed class PermissionDeniedException(string action) : Exception($"没有执行该操作的权限（{action}）。");
