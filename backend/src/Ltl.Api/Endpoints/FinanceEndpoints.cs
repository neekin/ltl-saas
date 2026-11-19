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
            var result = await service.GetReceivableAsync(RequireUserId(user), waybillId);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        finance.MapPost("/receivables/ensure", async (Guid waybillId, ClaimsPrincipal user, IFinanceService service) =>
            Results.Ok(await service.EnsureReceivableForOperatorAsync(RequireUserId(user), waybillId)));
        finance.MapPost("/payments", async (
            RecordPaymentRequest request,
            [Microsoft.AspNetCore.Mvc.FromHeader(Name = "Idempotency-Key")] Guid? idempotencyKey,
            ClaimsPrincipal user, IFinanceService service) =>
            Results.Ok(await service.RecordPaymentAsync(RequireUserId(user),
                request with { IdempotencyKey = idempotencyKey ?? request.IdempotencyKey })));
        finance.MapGet("/payments", async (Guid waybillId, ClaimsPrincipal user, IFinanceService service) =>
            Results.Ok(await service.ListPaymentsAsync(RequireUserId(user), waybillId)));
        finance.MapGet("/branch-payment-summary", async (string? month, ClaimsPrincipal user, IFinanceService service) =>
            Results.Ok(await service.GetBranchPaymentSummaryAsync(RequireUserId(user), month)));
        finance.MapPost("/refunds", async (RefundPaymentRequest request, ClaimsPrincipal user, IFinanceService service) =>
            Results.Ok(await service.RefundAsync(RequireUserId(user), request)));
        finance.MapPost("/statements", async (GenerateStatementRequest request, ClaimsPrincipal user, IFinanceService service) =>
        {
            var statement = await service.GenerateStatementAsync(
                RequireUserId(user), request.CustomerId, request.Period);
            return Results.Ok(new { statement.Id, statement.CustomerId, statement.Period, statement.TotalAmount, statement.Status });
        });
        finance.MapPost("/statements/{id:guid}/confirm", async (Guid id, ClaimsPrincipal user, IFinanceService service) =>
        {
            await service.ConfirmStatementAsync(RequireUserId(user), id);
            return Results.Ok(new { confirmed = true });
        });
        finance.MapPost("/statements/{id:guid}/close", async (Guid id, ClaimsPrincipal user, IFinanceService service) =>
        {
            await service.CloseStatementAsync(RequireUserId(user), id);
            return Results.Ok(new { closed = true });
        });
    }

    public sealed record GenerateStatementRequest(Guid CustomerId, string Period);

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new AuthenticationFailedException("无效的认证凭据");
}
