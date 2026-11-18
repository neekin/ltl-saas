using Ltl.Modules.Tenancy.Domain;
using Ltl.Modules.Tenancy.Infrastructure;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Tenancy.Application;

/// <summary>租户配额视图（TEN-002）。Max 为 null = 不限制。</summary>
public sealed record TenantQuotaDto(
    Guid TenantId, string? PlanName,
    int? MaxWaybills, int? MaxUsers, long? MaxStorageBytes);

/// <summary>
/// 租户配额服务（TEN-002）。SetQuota 需 quota.manage 权限（实时查库）；
/// CheckXxxCount 前置校验由各业务模块调用（开单/建用户/传文件）。
/// </summary>
public interface ITenantQuotaService
{
    Task SetQuotaAsync(Guid operatorUserId, SetTenantQuotaRequest request);

    /// <summary>当前租户配额（tenant.read 即可读）。</summary>
    Task<TenantQuotaDto> GetAsync();

    /// <summary>检查运单配额（currentCount 为业务模块聚合的当前用量）。超限抛 QuotaExceededException（409）。</summary>
    Task EnsureWaybillQuotaAsync(int currentCount);

    /// <summary>检查用户配额。</summary>
    Task EnsureUserQuotaAsync(int currentCount);

    /// <summary>检查存储配额（currentBytes 为现有文件字节合计；newBytes 为本次新增）。</summary>
    Task EnsureStorageQuotaAsync(long currentBytes, long newBytes);
}

public sealed record SetTenantQuotaRequest(
    Guid TenantId, string? PlanName, int? MaxWaybills, int? MaxUsers, long? MaxStorageBytes);

/// <summary>配额超限（API 层映射 409）。</summary>
public sealed class QuotaExceededException(string message) : Exception(message);

/// <summary>配额管理授权回调（依赖倒置：Tenancy 不反向依赖 Organization，组合根适配）。</summary>
public interface IQuotaAuthorizer
{
    Task<bool> HasPermissionAsync(Guid operatorUserId, string permissionCode);
}

public sealed class TenantQuotaService(
    TenancyDbContext db,
    ITenantContext tenantContext,
    IQuotaAuthorizer authorizer) : ITenantQuotaService
{
    public async Task SetQuotaAsync(Guid operatorUserId, SetTenantQuotaRequest request)
    {
        const string quotaManage = "quota.manage";
        if (!await authorizer.HasPermissionAsync(operatorUserId, quotaManage))
        {
            throw new QuotaPermissionDeniedException(quotaManage);
        }

        var tenant = await db.Tenants.FindAsync([request.TenantId])
            ?? throw new SharedKernel.Application.EntityNotFoundException("租户", request.TenantId);
        tenant.SetQuota(request.PlanName, request.MaxWaybills, request.MaxUsers, request.MaxStorageBytes,
            DateTime.UtcNow, operatorUserId);
        await db.SaveChangesAsync();
    }

    public async Task<TenantQuotaDto> GetAsync()
    {
        var tenantId = tenantContext.TenantId
            ?? throw new TenantContextMissingException();
        var tenant = await db.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == tenantId)
            ?? throw new SharedKernel.Application.EntityNotFoundException("租户", tenantId);
        return new TenantQuotaDto(tenant.Id, tenant.PlanName, tenant.MaxWaybills, tenant.MaxUsers, tenant.MaxStorageBytes);
    }

    public async Task EnsureWaybillQuotaAsync(int currentCount)
    {
        var quota = await LoadAsync();
        if (quota.MaxWaybills is { } max && currentCount >= max)
        {
            throw new QuotaExceededException($"套餐运单配额已用满（{currentCount}/{max}），请升级套餐后重试。");
        }
    }

    public async Task EnsureUserQuotaAsync(int currentCount)
    {
        var quota = await LoadAsync();
        if (quota.MaxUsers is { } max && currentCount >= max)
        {
            throw new QuotaExceededException($"套餐用户配额已用满（{currentCount}/{max}），请升级套餐后重试。");
        }
    }

    public async Task EnsureStorageQuotaAsync(long currentBytes, long newBytes)
    {
        var quota = await LoadAsync();
        if (quota.MaxStorageBytes is { } max && currentBytes + newBytes > max)
        {
            throw new QuotaExceededException(
                $"套餐存储配额不足（现有 {currentBytes} 字节 + 新增 {newBytes} 字节 > 上限 {max} 字节），请升级套餐或清理文件。");
        }
    }

    private async Task<Tenant> LoadAsync()
    {
        var tenantId = tenantContext.TenantId
            ?? throw new TenantContextMissingException();
        return await db.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == tenantId)
            ?? throw new SharedKernel.Application.EntityNotFoundException("租户", tenantId);
    }
}

/// <summary>配额管理权限拒绝（Tenancy 层异常，API 统一映射 403）。</summary>
public sealed class QuotaPermissionDeniedException(string action)
    : Exception($"没有执行该操作的权限（{action}）。");
