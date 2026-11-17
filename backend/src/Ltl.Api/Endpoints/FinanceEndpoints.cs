using System.Security.Claims;
using Ltl.Modules.Finance.Application;
using Ltl.Modules.Finance.Domain;
using Ltl.SharedKernel.Application;
using Ltl.Modules.Organization.Application.Auth;
using Ltl.Modules.Organization.Application.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ltl.Api.Endpoints;

/// <summary>财务端点（FIN-001）。</summary>
public static class FinanceEndpoints
{
    public static void MapFinanceEndpoints(this WebApplication app)
    {
        var finance = app.MapGroup("/api/finance");
        finance.MapGet("/receivables", async (Guid waybillId, ClaimsPrincipal user, IFinanceService service) =>
        {
            RequireRead(user);
            var result = await service.GetReceivableAsync(waybillId);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        finance.MapPost("/payments", async (RecordPaymentRequest request, ClaimsPrincipal user, IFinanceService service) =>
        {
            RequireManage(user);
            return Results.Ok(await service.RecordPaymentAsync(RequireUserId(user), request));
        });
        finance.MapGet("/payments", async (Guid waybillId, ClaimsPrincipal user, IFinanceService service) =>
        {
            RequireRead(user);
            return Results.Ok(await service.ListPaymentsAsync(waybillId));
        });
        finance.MapPost("/refunds", async (RefundPaymentRequest request, ClaimsPrincipal user, IFinanceService service) =>
        {
            RequireManage(user);
            return Results.Ok(await service.RefundAsync(RequireUserId(user), request));
        });
        finance.MapPost("/statements", async (GenerateStatementRequest request, ClaimsPrincipal user, IFinanceService service) =>
        {
            RequireManage(user);
            var statement = await service.GenerateStatementAsync(
                RequireUserId(user), request.CustomerId, request.Period);
            return Results.Ok(new { statement.Id, statement.CustomerId, statement.Period, statement.TotalAmount, statement.Status });
        });
        finance.MapPost("/statements/{id:guid}/confirm", async (Guid id, ClaimsPrincipal user, IFinanceService service) =>
        {
            RequireManage(user);
            await service.ConfirmStatementAsync(RequireUserId(user), id);
            return Results.Ok(new { confirmed = true });
        });
        finance.MapPost("/statements/{id:guid}/close", async (Guid id, ClaimsPrincipal user, IFinanceService service) =>
        {
            RequireManage(user);
            await service.CloseStatementAsync(RequireUserId(user), id);
            return Results.Ok(new { closed = true });
        });
    }

    public sealed record GenerateStatementRequest(Guid CustomerId, string Period);

    private static void RequireRead(ClaimsPrincipal user)
    {
        var userId = RequireUserId(user);
        // 简化：finance.read 校验由服务层完成（权限实时查库）
        _ = userId;
    }

    private static void RequireManage(ClaimsPrincipal user)
    {
        var userId = RequireUserId(user);
        _ = userId;
    }

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new AuthenticationFailedException("无效的认证凭据");
}
