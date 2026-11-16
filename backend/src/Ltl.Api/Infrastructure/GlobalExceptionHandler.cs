using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Ltl.Api.Infrastructure;

/// <summary>
/// 全局异常处理器：任何未处理异常统一转换为 RFC 9457 ProblemDetails（AGENTS.md §8）。
/// 不向客户端泄露堆栈、SQL 或内部敏感信息；响应仅携带 traceId 供日志定位。
/// </summary>
internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "未处理异常：{Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        var problem = Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "服务器内部错误",
            type: "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = httpContext.TraceIdentifier,
            });

        await problem.ExecuteAsync(httpContext);
        return true;
    }
}
