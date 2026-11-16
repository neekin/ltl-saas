namespace Ltl.SharedKernel.Application;

/// <summary>
/// 租户上下文的默认实现（scoped）：
/// - HTTP 请求内由中间件解析后写入显式租户；
/// - 后台任务在新 scope 内显式 SetTenant，不依赖线程隐式状态（AGENTS.md §4.3）。
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private Guid? _tenantId;

    public Guid? TenantId => _tenantId;

    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("租户 Id 不能为空 Guid。", nameof(tenantId));
        }

        _tenantId = tenantId;
    }

    public void ClearTenant() => _tenantId = null;

    public Guid RequireTenant()
    {
        if (_tenantId is { } tenantId)
        {
            return tenantId;
        }

        throw new TenantContextMissingException();
    }
}
