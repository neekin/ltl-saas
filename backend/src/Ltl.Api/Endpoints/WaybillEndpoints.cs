using System.Security.Claims;
using Ltl.Modules.Organization.Application.Auth;
using Ltl.Modules.Waybills.Application;
using Ltl.Modules.Waybills.Infrastructure;
using Ltl.Modules.Waybills.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ltl.Api.Endpoints;

/// <summary>运单端点（WB-PRINT：打印数据；后续开单/确认/取消端点随业务任务追加）。</summary>
public static class WaybillEndpoints
{
    public static void MapWaybillEndpoints(this WebApplication app)
    {
        app.MapGet("/api/waybills/{id:guid}/print", async (
            Guid id, ClaimsPrincipal user, IWaybillPrintService service) =>
            Results.Ok(await service.GetPrintViewAsync(RequireUserId(user), id)));

        // PRINT-001：ESC/POS 热敏指令导出（.prn 下载，打印代理/热敏打印机对接）
        app.MapGet("/api/waybills/{id:guid}/print/escpos", async (
            Guid id, ClaimsPrincipal user, IWaybillPrintService service) =>
        {
            var view = await service.GetPrintViewAsync(RequireUserId(user), id);
            var bytes = EscPosLabelBuilder.Build(view);
            return Results.File(bytes, "application/octet-stream",
                $"waybill-{view.WaybillNo}.prn");
        });

        // WB-002：开单（补暴露服务层能力）。幂等：Idempotency-Key header（AGENTS §8）
        app.MapPost("/api/waybills", async (
            CreateWaybillRequest request,
            [Microsoft.AspNetCore.Mvc.FromHeader(Name = "Idempotency-Key")] Guid? idempotencyKey,
            ClaimsPrincipal user, IWaybillService service) =>
            Results.Ok(await service.CreateWaybillAsync(new CreateWaybillCommand(
                request.OriginBranchId, request.DestBranchId, request.CustomerId,
                request.PaymentType, request.SenderName, request.SenderPhone, request.SenderAddress,
                request.ReceiverName, request.ReceiverPhone, request.ReceiverAddress,
                request.Packages.Select(p => new WaybillPackageDraft(
                    p.CargoName, p.Quantity, p.WeightKg, p.VolumeM3)).ToArray(),
                request.RuleSetId, DateOnly.FromDateTime(DateTime.UtcNow),
                CodAmount: request.CodAmount,
                Remark: request.Remark, DeclaredValue: request.DeclaredValue,
                BranchId: request.OriginBranchId,
                OperatorId: RequireUserId(user), IdempotencyKey: idempotencyKey))));

        // WB-003：查询/详情/草稿修改/确认/取消
        var waybills = app.MapGroup("/api/waybills");
        waybills.MapGet("/", async (
            int? page, int? pageSize, string? waybillNo, ClaimsPrincipal user, IWaybillAdminService service) =>
            Results.Ok(await service.ListAsync(RequireUserId(user), page ?? 1, pageSize ?? 20, waybillNo)));
        waybills.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IWaybillAdminService service) =>
            Results.Ok(await service.GetAsync(RequireUserId(user), id)));
        waybills.MapPut("/{id:guid}/draft", async (
            Guid id, UpdateWaybillDraftRequest request, ClaimsPrincipal user, IWaybillAdminService service) =>
            Results.Ok(await service.UpdateDraftAsync(RequireUserId(user), id, request)));
        waybills.MapGet("/{id:guid}/events", async (Guid id, ClaimsPrincipal user, IWaybillAdminService service) =>
            Results.Ok(await service.ListEventsAsync(RequireUserId(user), id)));
        waybills.MapPost("/{id:guid}/confirm", async (Guid id, ClaimsPrincipal user, IWaybillAdminService service) =>
            Results.Ok(await service.ConfirmAsync(RequireUserId(user), id)));
        waybills.MapPost("/{id:guid}/cancel", async (
            Guid id, CancelWaybillRequest request, ClaimsPrincipal user, IWaybillAdminService service) =>
            Results.Ok(await service.CancelAsync(RequireUserId(user), id, request.Reason)));

        // P10 客户查询：凭租户编码 + 运单号 + 查询码匿名查单票（脱敏）；防枚举统一 401。
        // 畸形请求（无 body / 空字段）同样按 401 处理，交由服务层统一校验，避免 500 与信息差异。
        app.MapPost("/api/public/waybill-query", async (
            CustomerQueryRequest? request, IWaybillQueryCodeService service) =>
        {
            try
            {
                return Results.Ok(await service.QueryAsync(
                    request?.TenantCode ?? string.Empty,
                    request?.WaybillNo ?? string.Empty,
                    request?.QueryCode ?? string.Empty));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        }).AllowAnonymous();

        // P10 内部重置查询码：需登录 + waybill.read（服务层校验，权限异常由全局 handler 映射 403）。
        app.MapPost("/api/waybills/{id:guid}/query-code", async (
            Guid id, ClaimsPrincipal user, IWaybillQueryCodeService service) =>
            Results.Ok(new { queryCode = await service.ResetAsync(RequireUserId(user), id) }));
    }

    public sealed record CustomerQueryRequest(string TenantCode, string WaybillNo, string QueryCode);

    public sealed record CancelWaybillRequest(string? Reason);

    /// <summary>开单请求（WB-002）。支付方式按枚举名（Cash/OnDelivery/Monthly）由 JsonStringEnumConverter 绑定。</summary>
    public sealed record CreateWaybillRequest(
        Guid OriginBranchId,
        Guid DestBranchId,
        Guid CustomerId,
        Guid RuleSetId,
        WaybillPaymentType PaymentType,
        string SenderName,
        string SenderPhone,
        string SenderAddress,
        string ReceiverName,
        string ReceiverPhone,
        string ReceiverAddress,
        IReadOnlyList<CreatePackageRequest> Packages,
        string? Remark = null,
        decimal? DeclaredValue = null,
        decimal? CodAmount = null);

    public sealed record CreatePackageRequest(
        string CargoName, int Quantity, decimal WeightKg, decimal VolumeM3);

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new AuthenticationFailedException("无效的认证凭据");
}
