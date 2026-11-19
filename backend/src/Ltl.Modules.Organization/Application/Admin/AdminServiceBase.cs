using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Organization.Infrastructure;
using Ltl.Modules.Platform.Application;
using Ltl.SharedKernel.Application;

namespace Ltl.Modules.Organization.Application.Admin;

/// <summary>分页结果（ORG-003 统一分页约定）。</summary>
public sealed record PageResult<T>(
    IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>
/// 组织管理服务公共基类：权限点实时校验（禁用即时生效）、租户上下文、审计写入。
/// 写操作由实现方以 TransactionScope 包裹业务保存与审计（同库环境事务，ORG-003 设计 §6）。
/// </summary>
public abstract class AdminServiceBase(
    OrganizationDbContext db,
    IAuthorizationService authorization,
    IAuditLogService audit)
{
    protected readonly OrganizationDbContext Db = db;
    protected readonly IAuthorizationService Authorization = authorization;

    protected Guid RequireTenantId() =>
        Db.CurrentTenantId ?? throw new TenantContextMissingException();

    /// <summary>权限点实时校验；失败 → PermissionDeniedException（403 语义）。</summary>
    protected async Task RequirePermissionAsync(Guid operatorUserId, string permissionCode, string action)
    {
        if (!await Authorization.HasPermissionAsync(operatorUserId, permissionCode))
        {
            throw new PermissionDeniedException(action);
        }
    }

    /// <summary>记录审计（在调用方环境事务内）。</summary>
    protected Task RecordAuditAsync(string action, string entityType, Guid? entityId, Guid operatorUserId, object detail) =>
        audit.RecordAsync(action, entityType, entityId, operatorUserId, detail);

    /// <summary>规范分页参数（page ≥1，pageSize 1..100 默认 20）。</summary>
    protected static (int Page, int PageSize, int Skip) NormalizePaging(int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
        return (page, pageSize, (page - 1) * pageSize);
    }
}
