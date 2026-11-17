using System.Transactions;
using Ltl.Modules.Delivery.Domain;
using Ltl.Modules.Delivery.Infrastructure;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Platform.Application;
using Ltl.Modules.Waybills.Application;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Delivery.Application;

public sealed record DeliveryOrderDto(
    Guid Id, Guid WaybillId, Guid BranchId, string DeliveryType, string Status,
    Guid? DriverId, string? ReceiverName, Guid? OperatorUserId,
    DateTime? CompletedAt, DateTime CreatedAt);

public sealed record CreateDeliveryRequest(
    Guid WaybillId, Guid BranchId, string DeliveryType, Guid? DriverId);

public sealed record CompleteDeliveryRequest(string ReceiverName, Guid? PhotoFileId);

public interface IDeliveryService
{
    Task<DeliveryOrderDto> CreateAsync(Guid operatorUserId, CreateDeliveryRequest request);
    Task<DeliveryOrderDto> CompleteAsync(Guid operatorUserId, Guid deliveryOrderId, CompleteDeliveryRequest request);
    Task<DeliveryOrderDto> RejectAsync(Guid operatorUserId, Guid deliveryOrderId);
    Task<DeliveryOrderDto?> GetByWaybillAsync(Guid operatorUserId, Guid waybillId);
}

/// <summary>
/// 交付服务实现（DELIVERY-001）。组合授权（delivery.manage + 交付网点范围实时查库）；
/// 运单状态经 Waybills 聚合守卫；签收后 POD 落库 + 运费财务状态不变（AGENTS §6.1）；
/// 交付单/POD/事件/审计同库环境事务。
/// </summary>
public sealed class DeliveryService(
    DeliveryDbContext db,
    IWaybillCargoService waybillCargoService,
    IAuthorizationService authorization,
    IAuditLogService audit) : IDeliveryService
{
    public async Task<DeliveryOrderDto> CreateAsync(Guid operatorUserId, CreateDeliveryRequest request)
    {
        var tenantId = db.CurrentTenantId
            ?? throw new TenantContextMissingException();

        if (!await authorization.AuthorizeAsync(
                operatorUserId, request.BranchId, DeliveryPermissionCodes.DeliveryManage))
        {
            throw new PermissionDeniedException(DeliveryPermissionCodes.DeliveryManage);
        }

        // 运单交付状态守卫：仅待提货可创建交付单
        var snapshot = await waybillCargoService.GetSnapshotAsync(request.WaybillId);
        if (snapshot.DeliveryStatus != Ltl.Modules.Waybills.Domain.WaybillDeliveryStatus.WaitingPickup)
        {
            throw new InvalidOperationException(
                $"运单 {snapshot.WaybillNo} 交付状态为 {snapshot.DeliveryStatus}，仅待提货运单可创建交付单。");
        }

        var deliveryType = request.DeliveryType.Trim().ToLowerInvariant() == "delivery"
            ? DeliveryType.Delivery
            : DeliveryType.SelfPickup;

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var order = DeliveryOrder.Create(
            tenantId, request.WaybillId, request.BranchId, deliveryType,
            request.DriverId, operatorUserId, DateTime.UtcNow, operatorUserId);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        db.Events.Add(DeliveryEvent.Create(
            tenantId, order.Id, "created",
            System.Text.Json.JsonSerializer.Serialize(
                new { waybillId = request.WaybillId, deliveryType = deliveryType.ToString(), branchId = request.BranchId },
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }),
            operatorUserId, DateTime.UtcNow));
        await db.SaveChangesAsync();

        await audit.RecordAsync(
            "delivery.create", "delivery_order", order.Id, operatorUserId,
            new { waybillId = request.WaybillId, deliveryType = deliveryType.ToString() });
        scope.Complete();

        return ToDto(order);
    }

    public async Task<DeliveryOrderDto> CompleteAsync(
        Guid operatorUserId, Guid deliveryOrderId, CompleteDeliveryRequest request)
    {
        var order = await db.Orders.FindAsync([deliveryOrderId])
            ?? throw new EntityNotFoundException("交付单", deliveryOrderId);

        if (!await authorization.AuthorizeAsync(
                operatorUserId, order.BranchId, DeliveryPermissionCodes.DeliveryManage))
        {
            throw new PermissionDeniedException(DeliveryPermissionCodes.DeliveryManage);
        }

        var now = DateTime.UtcNow;

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        order.Complete(request.ReceiverName, now, operatorUserId);
        await db.SaveChangesAsync();

        // 运单交付状态流转（聚合守卫：仅 WaitingPickup → Delivered）
        await waybillCargoService.MarkDeliveredAsync(order.WaybillId, operatorUserId,
            $"交付签收：签收人 {request.ReceiverName}");

        // POD 落库（运费财务状态不变，AGENTS §6.1）
        db.Proofs.Add(ProofOfDelivery.Create(
            order.TenantId, order.Id, request.ReceiverName, request.PhotoFileId, now));
        db.Events.Add(DeliveryEvent.Create(
            order.TenantId, order.Id, "completed",
            System.Text.Json.JsonSerializer.Serialize(
                new { receiverName = request.ReceiverName, photoFileId = request.PhotoFileId },
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }),
            operatorUserId, now));
        await db.SaveChangesAsync();

        await audit.RecordAsync(
            "delivery.complete", "delivery_order", order.Id, operatorUserId,
            new { waybillId = order.WaybillId, receiverName = request.ReceiverName });
        scope.Complete();

        return ToDto(order);
    }

    public async Task<DeliveryOrderDto> RejectAsync(Guid operatorUserId, Guid deliveryOrderId)
    {
        var order = await db.Orders.FindAsync([deliveryOrderId])
            ?? throw new EntityNotFoundException("交付单", deliveryOrderId);

        if (!await authorization.AuthorizeAsync(
                operatorUserId, order.BranchId, DeliveryPermissionCodes.DeliveryManage))
        {
            throw new PermissionDeniedException(DeliveryPermissionCodes.DeliveryManage);
        }

        var now = DateTime.UtcNow;

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        order.Reject(now, operatorUserId);
        await db.SaveChangesAsync();

        await waybillCargoService.MarkRejectedAsync(order.WaybillId, operatorUserId,
            $"交付拒收（网点 {order.BranchId}）");

        db.Events.Add(DeliveryEvent.Create(
            order.TenantId, order.Id, "rejected", null, operatorUserId, now));
        await db.SaveChangesAsync();

        await audit.RecordAsync(
            "delivery.reject", "delivery_order", order.Id, operatorUserId,
            new { waybillId = order.WaybillId });
        scope.Complete();

        return ToDto(order);
    }

    public async Task<DeliveryOrderDto?> GetByWaybillAsync(Guid operatorUserId, Guid waybillId)
    {
        if (db.CurrentTenantId is null)
            throw new TenantContextMissingException();
        if (!await authorization.HasPermissionAsync(operatorUserId, DeliveryPermissionCodes.DeliveryRead))
            throw new PermissionDeniedException(DeliveryPermissionCodes.DeliveryRead);

        var order = await db.Orders
            .AsNoTracking()
            .SingleOrDefaultAsync(o => o.WaybillId == waybillId);
        return order is null ? null : ToDto(order);
    }

    private static DeliveryOrderDto ToDto(DeliveryOrder order) => new(
        order.Id, order.WaybillId, order.BranchId, order.DeliveryType.ToString(),
        order.Status.ToString(), order.DriverId, order.ReceiverName,
        order.OperatorUserId, order.CompletedAt, order.CreatedAt);
}
