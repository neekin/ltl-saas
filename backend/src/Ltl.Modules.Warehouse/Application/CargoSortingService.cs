using System.Transactions;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Platform.Application;
using Ltl.Modules.Warehouse.Domain;
using Ltl.Modules.Warehouse.Infrastructure;
using Ltl.Modules.Waybills.Application;
using Ltl.Modules.Waybills.Domain;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ltl.Modules.Warehouse.Application;

public sealed record CargoSortingTaskDto(
    Guid Id, Guid WaybillId, Guid BranchId, string Status,
    Guid? OperatorUserId, DateTime? CompletedAt, Guid? CompletedBy, string? Note, DateTime CreatedAt);

public interface ICargoSortingService
{
    /// <summary>创建分拣任务（仅货物状态已收货；每运单同时最多一个未完成任务）。</summary>
    Task<CargoSortingTaskDto> CreateAsync(Guid operatorUserId, Guid waybillId, Guid branchId, string? note);

    /// <summary>完成任务：货物状态 已收货 → 已分拣 + 事件 + 审计（同环境事务）。</summary>
    Task<CargoSortingTaskDto> CompleteAsync(Guid operatorUserId, Guid taskId);

    /// <summary>运单任务列表（时间正序）。</summary>
    Task<IReadOnlyList<CargoSortingTaskDto>> ListByWaybillAsync(Guid operatorUserId, Guid waybillId);
}

/// <summary>
/// 分拣任务实现（WAREHOUSE-001）。组合授权（sort 权限 + 网点范围实时校验）；
/// 状态守卫经 Waybills 公开接口（聚合守卫）；任务/事件/审计同库环境事务。
/// </summary>
public sealed class CargoSortingService(
    WarehouseDbContext db,
    IWaybillCargoService waybillCargoService,
    IAuthorizationService authorization,
    IAuditLogService audit) : ICargoSortingService
{
    public async Task<CargoSortingTaskDto> CreateAsync(
        Guid operatorUserId, Guid waybillId, Guid branchId, string? note)
    {
        var tenantId = db.CurrentTenantId
            ?? throw new TenantContextMissingException();

        if (!await authorization.AuthorizeAsync(operatorUserId, branchId, WarehousePermissionCodes.CargoSort))
        {
            throw new PermissionDeniedException(WarehousePermissionCodes.CargoSort);
        }

        // 状态守卫：仅已收货运单可分拣（聚合侧在完成时再校验，双保险）
        var snapshot = await waybillCargoService.GetSnapshotAsync(waybillId);
        if (snapshot.CargoStatus != WaybillCargoStatus.Received)
        {
            throw new WarehouseDomainException(
                $"运单 {snapshot.WaybillNo} 货物状态为 {snapshot.CargoStatus}，仅已收货运单可创建分拣任务。");
        }

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var task = CargoHandlingTask.CreateSorting(
            tenantId, waybillId, branchId, operatorUserId, note, DateTime.UtcNow, operatorUserId);
        db.HandlingTasks.Add(task);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "ux_cargo_handling_tasks_active",
            })
        {
            // 部分唯一索引兜底并发：每运单同时最多一个未完成任务 → 409
            throw new ConflictException("该运单已存在未完成的分拣任务。");
        }

        await audit.RecordAsync(
            "warehouse.sorting.create", "cargo_handling_task", task.Id, operatorUserId,
            new { waybillNo = snapshot.WaybillNo, branchId });
        scope.Complete();

        return ToDto(task);
    }

    public async Task<CargoSortingTaskDto> CompleteAsync(Guid operatorUserId, Guid taskId)
    {
        var task = await db.HandlingTasks.FindAsync([taskId])
            ?? throw new EntityNotFoundException("分拣任务", taskId);

        if (!await authorization.AuthorizeAsync(operatorUserId, task.BranchId, WarehousePermissionCodes.CargoSort))
        {
            throw new PermissionDeniedException(WarehousePermissionCodes.CargoSort);
        }

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        task.Complete(operatorUserId, DateTime.UtcNow);
        await db.SaveChangesAsync();

        // 货物状态流转 + 事件（聚合守卫；返工场景已为 Sorted → 领域方法拒绝重复流转）
        await waybillCargoService.MarkCargoSortedAsync(
            task.WaybillId, operatorUserId, $"分拣完成（任务 {task.Id}，网点 {task.BranchId}）");

        await audit.RecordAsync(
            "warehouse.sorting.complete", "cargo_handling_task", task.Id, operatorUserId,
            new { waybillId = task.WaybillId, branchId = task.BranchId });
        scope.Complete();

        return ToDto(task);
    }

    public async Task<IReadOnlyList<CargoSortingTaskDto>> ListByWaybillAsync(Guid operatorUserId, Guid waybillId)
    {
        if (db.CurrentTenantId is null)
        {
            throw new TenantContextMissingException();
        }

        if (!await authorization.HasPermissionAsync(operatorUserId, WarehousePermissionCodes.CargoRead))
        {
            throw new PermissionDeniedException(WarehousePermissionCodes.CargoRead);
        }

        return (await db.HandlingTasks
                .Where(t => t.WaybillId == waybillId)
                .OrderBy(t => t.CreatedAt)
                .ToListAsync())
            .Select(ToDto)
            .ToList();
    }

    private static CargoSortingTaskDto ToDto(CargoHandlingTask task) => new(
        task.Id, task.WaybillId, task.BranchId, task.Status.ToString(),
        task.OperatorUserId, task.CompletedAt, task.CompletedBy, task.Note, task.CreatedAt);
}
