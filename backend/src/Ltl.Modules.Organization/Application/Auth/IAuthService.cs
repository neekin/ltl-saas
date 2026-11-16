namespace Ltl.Modules.Organization.Application.Auth;

/// <summary>登录响应（权限点为登录时点快照，仅前端菜单展示用；后端授权始终实时查库）。</summary>
public sealed record LoginResult(
    string AccessToken,
    int ExpiresInSeconds,
    string RefreshToken,
    Guid UserId,
    string Username,
    string DisplayName,
    Guid TenantId,
    IReadOnlyList<string> Permissions);

/// <summary>刷新响应（轮换：返回全新 access + refresh）。</summary>
public sealed record RefreshResult(string AccessToken, int ExpiresInSeconds, string RefreshToken);

/// <summary>
/// 认证服务（AUTH-001）：登录（租户码+用户名+密码）、刷新轮换、退出撤销、当前用户。
/// </summary>
public interface IAuthService
{
    /// <exception cref="AuthenticationFailedException">统一 401 语义：租户/用户不存在、密码错误、已停用。</exception>
    Task<LoginResult> LoginAsync(string tenantCode, string username, string password);

    /// <exception cref="AuthenticationFailedException">令牌无效/过期/已撤销/重放，或用户/租户已停用。</exception>
    Task<RefreshResult> RefreshAsync(string refreshToken);

    /// <summary>撤销刷新令牌（幂等：不存在也成功）。</summary>
    Task LogoutAsync(string refreshToken);

    /// <summary>当前已认证用户信息（含实时权限点）；用户不存在或已停用 → 认证失败。</summary>
    /// <exception cref="AuthenticationFailedException">用户不存在或已停用。</exception>
    Task<(Guid UserId, string Username, string DisplayName, Guid TenantId, IReadOnlyList<string> Permissions)>
        GetCurrentUserAsync(Guid userId);
}
