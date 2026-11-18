namespace Ltl.Modules.Platform.Domain;

/// <summary>Platform 模块权限拒绝（依赖倒置：不引用 Organization 的同名异常，API 层统一映射 403）。</summary>
public sealed class PlatformPermissionDeniedException(string action)
    : Exception($"没有执行该操作的权限（{action}）。");
