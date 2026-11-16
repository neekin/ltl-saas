using Ltl.Modules.Tenancy.Application;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Ltl.Modules.Tenancy.Infrastructure;

/// <summary>
/// 租户上下文缺失异常 → 401 ProblemDetails（统一错误格式，AGENTS.md §8）。
/// </summary>
public sealed class TenantContextMissingExceptionHandler(ILogger<TenantContextMissingExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not TenantContextMissingException)
        {
            return false;
        }

        logger.LogWarning(
            "租户上下文缺失：{Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        var problem = Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "未提供有效的租户上下文",
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = httpContext.TraceIdentifier,
            });

        await problem.ExecuteAsync(httpContext);
        return true;
    }
}
