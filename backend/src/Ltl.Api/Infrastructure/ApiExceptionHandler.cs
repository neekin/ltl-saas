using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Pricing.Domain;
using Ltl.Modules.Transport.Domain;
using Ltl.Modules.Warehouse.Domain;
using Ltl.Modules.Waybills.Domain;
using Ltl.SharedKernel.Application;
using Microsoft.AspNetCore.Diagnostics;

namespace Ltl.Api.Infrastructure;

/// <summary>
/// API 错误语义处理器（ORG-003）：把跨模块通用异常映射为对应 ProblemDetails 状态码。
/// 必须注册在兜底 GlobalExceptionHandler 之前（IExceptionHandler 按注册顺序匹配）。
/// 异常消息均为业务校验文案（无内部细节），可安全返回客户端。
/// </summary>
public abstract class ApiExceptionHandler<T> : IExceptionHandler where T : Exception
{
    /// <summary>RFC 9110 状态码 → 语义章节（type URI 仅辅助定位，不影响语义）。</summary>
    private static readonly IReadOnlyDictionary<int, string> Rfc9110Sections = new Dictionary<int, string>
    {
        [StatusCodes.Status400BadRequest] = "15.5.1",
        [StatusCodes.Status403Forbidden] = "15.5.4",
        [StatusCodes.Status404NotFound] = "15.5.5",
        [StatusCodes.Status409Conflict] = "15.5.10",
    };

    protected abstract int StatusCode { get; }

    protected virtual string Title(T exception) => exception.Message;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not T typed || !Rfc9110Sections.TryGetValue(StatusCode, out var section))
        {
            return false;
        }

        context.Response.StatusCode = StatusCode;
        await context.Response.WriteAsJsonAsync(
            new
            {
                type = $"https://tools.ietf.org/html/rfc9110#section-{section}",
                title = Title(typed),
                status = StatusCode,
                traceId = context.TraceIdentifier,
            },
            (System.Text.Json.JsonSerializerOptions?)null,
            "application/problem+json",
            cancellationToken);
        return true;
    }
}

/// <summary>无权限点 → 403（已认证但权限实时校验失败，禁用即时生效）。</summary>
public sealed class PermissionDeniedExceptionHandler : ApiExceptionHandler<PermissionDeniedException>
{
    protected override int StatusCode => StatusCodes.Status403Forbidden;
}

/// <summary>实体不存在或跨租户不可见 → 404。</summary>
public sealed class EntityNotFoundExceptionHandler : ApiExceptionHandler<EntityNotFoundException>
{
    protected override int StatusCode => StatusCodes.Status404NotFound;
}

/// <summary>资源冲突（编码重复等）→ 409。</summary>
public sealed class ConflictExceptionHandler : ApiExceptionHandler<ConflictException>
{
    protected override int StatusCode => StatusCodes.Status409Conflict;
}

/// <summary>领域参数违规 → 400（消息为业务校验文案）。</summary>
public sealed class ArgumentValidationExceptionHandler : ApiExceptionHandler<ArgumentException>
{
    protected override int StatusCode => StatusCodes.Status400BadRequest;
}

/// <summary>运单状态机违规（收货/分拣/确认/取消守卫）→ 409。</summary>
public sealed class WaybillDomainViolationHandler : ApiExceptionHandler<WaybillDomainException>
{
    protected override int StatusCode => StatusCodes.Status409Conflict;
}

/// <summary>计价规则违规（无发布版本/停用/校验失败）→ 409。</summary>
public sealed class PricingRuleViolationHandler : ApiExceptionHandler<PricingRuleViolationException>
{
    protected override int StatusCode => StatusCodes.Status409Conflict;
}

/// <summary>仓储作业违规（未收货分拣、重复完成等）→ 409。</summary>
public sealed class WarehouseDomainViolationHandler : ApiExceptionHandler<WarehouseDomainException>
{
    protected override int StatusCode => StatusCodes.Status409Conflict;
}

/// <summary>运输域违规（发运后配载/撤销等）→ 409。</summary>
public sealed class TransportDomainViolationHandler : ApiExceptionHandler<TransportDomainException>
{
    protected override int StatusCode => StatusCodes.Status409Conflict;
}
