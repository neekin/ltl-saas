namespace Ltl.Modules.Tenancy.Application;

/// <summary>
/// 租户上下文：请求内由 TenantResolutionMiddleware 从已认证 claim 解析；
/// 后台任务必须显式 <see cref="SetTenant"/> 建立上下文（AGENTS.md §4.3）。
/// </summary>
public interface ITenantContext
{
    /// <summary>当前租户 Id；未解析出租户时为 null。</summary>
    Guid? TenantId { get; }

    /// <summary>显式设置租户上下文（仅限后台任务/测试；HTTP 请求内由中间件调用）。</summary>
    void SetTenant(Guid tenantId);

    /// <summary>清除显式设置的租户上下文。</summary>
    void ClearTenant();

    /// <summary>
    /// 取得当前租户 Id；缺失时抛出 <see cref="TenantContextMissingException"/>。
    /// 所有租户业务端点必须通过本方法取得租户，禁止自行读 claim。
    /// </summary>
    Guid RequireTenant();
}
