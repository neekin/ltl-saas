namespace Ltl.SharedKernel.Application;

/// <summary>
/// JWT claim 契约（ADR-0005）：P1 登录任务签发 token 时必须遵守。
/// 租户标识只能来自已认证身份的 claim，禁止从 Body/Query/Header 读取（AGENTS.md §5.1）。
/// </summary>
public static class TenantClaimTypes
{
    public const string TenantId = "tenant_id";
}
