namespace Ltl.SharedKernel.Application;

/// <summary>
/// 租户上下文缺失异常：由 Tenancy 模块的统一异常处理映射为 401 ProblemDetails。
/// </summary>
public sealed class TenantContextMissingException : Exception
{
    public TenantContextMissingException(string message = "当前请求未携带有效的租户上下文。")
        : base(message)
    {
    }
}
