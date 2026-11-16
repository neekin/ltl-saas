using System.Security.Claims;
using Ltl.Modules.Tenancy.Application;
using Microsoft.AspNetCore.Http;

namespace Ltl.Modules.Tenancy.Infrastructure;

/// <summary>
/// 租户解析中间件：从已认证身份的 tenant_id claim 解析租户上下文（ADR-0005）。
/// claim 非法时返回 401 并终止管道；未认证或无 claim 时上下文为空，业务端点经 RequireTenant() 拒绝。
/// 必须注册在 UseAuthentication 之后（P1 登录任务接入认证中间件时保持该顺序）。
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var claim = context.User.FindFirstValue(TenantClaimTypes.TenantId);

            if (claim is not null)
            {
                if (Guid.TryParse(claim, out var tenantId) && tenantId != Guid.Empty)
                {
                    tenantContext.SetTenant(tenantId);
                }
                else
                {
                    // 直接手写 application/problem+json，避免对请求 DI 服务集合的依赖
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(
                        new
                        {
                            type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                            title = "无效的租户标识",
                            status = 401,
                            traceId = context.TraceIdentifier,
                        },
                        (System.Text.Json.JsonSerializerOptions?)null,
                        "application/problem+json");
                    return;
                }
            }
        }

        await next(context);
    }
}
