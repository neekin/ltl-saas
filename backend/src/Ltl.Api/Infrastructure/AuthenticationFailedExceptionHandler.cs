using Ltl.Modules.Organization.Application.Auth;
using Microsoft.AspNetCore.Diagnostics;

namespace Ltl.Api.Infrastructure;

/// <summary>
/// 认证失败 → 401 application/problem+json。异常消息本身即安全的外部信息（防枚举统一文案，
/// 不含内部细节）。必须注册在兜底 GlobalExceptionHandler 之前（IExceptionHandler 按注册顺序匹配）。
/// </summary>
public sealed class AuthenticationFailedExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not AuthenticationFailedException)
        {
            return false;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(
            new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                title = exception.Message,
                status = 401,
                traceId = context.TraceIdentifier,
            },
            (System.Text.Json.JsonSerializerOptions?)null,
            "application/problem+json",
            cancellationToken);
        return true;
    }
}
