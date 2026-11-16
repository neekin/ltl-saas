using System.Security.Cryptography;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Organization.Infrastructure;
using Ltl.SharedKernel.Application;
using Ltl.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ltl.Modules.Organization.Application.Auth;

/// <summary>
/// 认证实现（AUTH-001）。租户定位用原生 SQL（同库、显式参数，不引用 Tenancy 模块 DbContext）；
/// 登录失败统一异常（防枚举）；刷新令牌轮换 + 用户/租户状态实时校验（禁用即时生效）。
/// 登录/刷新时租户上下文本不存在（匿名请求，过滤器 fail-closed）——定位到租户后
/// 由服务端显式 SetTenant（tenant_id 来自服务端查库结果，非客户端提交，AGENTS §5.1），
/// 用户与权限查询随即在租户过滤内进行。
/// </summary>
public sealed class AuthService(
    OrganizationDbContext db,
    ITenantContext tenantContext,
    IAuthorizationService authorizationService,
    IJwtTokenIssuer jwtTokenIssuer,
    IOptions<JwtOptions> jwtOptions) : IAuthService
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<LoginResult> LoginAsync(string tenantCode, string username, string password)
    {
        // 统一失败信息（防枚举）：租户不存在/用户不存在/密码错误/已停用 → 同一响应
        const string failureMessage = "登录名或密码不正确";

        var tenantId = (await db.Database
                .SqlQuery<TenantRow>(
                    $"""SELECT id AS "Id", status AS "Status" FROM tenants WHERE code = {tenantCode.Trim()}""")
                .ToListAsync())
            .SingleOrDefault() is { } tenant && tenant.Status == 0 // TenantStatus.Active
            ? tenant.Id
            : throw new AuthenticationFailedException(failureMessage);

        // 服务端建立租户上下文：后续用户/权限查询经租户过滤器（仅限该租户）
        tenantContext.SetTenant(tenantId);

        var user = await db.Users
            .SingleOrDefaultAsync(u => u.TenantId == tenantId && u.Username == username.Trim());
        if (user is null || user.Status != UserStatus.Active)
        {
            throw new AuthenticationFailedException(failureMessage);
        }

        if (!PasswordHasher.Verify(password, user.PasswordHash))
        {
            throw new AuthenticationFailedException(failureMessage);
        }

        var permissions = (await authorizationService.GetPermissionCodesAsync(user.Id)).ToList();
        var (accessToken, expiresIn) = jwtTokenIssuer.IssueAccessToken(user.TenantId, user.Id, user.Username);
        var refreshToken = await IssueRefreshTokenAsync(user.TenantId, user.Id);

        return new LoginResult(
            accessToken, expiresIn, refreshToken,
            user.Id, user.Username, user.DisplayName, user.TenantId, permissions);
    }

    public async Task<RefreshResult> RefreshAsync(string refreshToken)
    {
        var stored = await db.RefreshTokens.FindAsync([HashToken(refreshToken)]);
        if (stored is null || !stored.IsActive(DateTime.UtcNow))
        {
            throw new AuthenticationFailedException("无效的刷新令牌");
        }

        // 服务端建立租户上下文（令牌已绑定租户），用户查询经过滤器
        tenantContext.SetTenant(stored.TenantId);

        var user = await db.Users
            .SingleOrDefaultAsync(u => u.TenantId == stored.TenantId && u.Id == stored.UserId);
        if (user is null || user.Status != UserStatus.Active)
        {
            throw new AuthenticationFailedException("无效的刷新令牌");
        }

        // 租户停用后刷新立即失效（实时校验）。
        // ToListAsync 原样执行：对未映射类型 SqlQuery 组合 LINQ 会包装子查询，外层引用
        // 带引号的大小写敏感别名（"Status"）导致 42703（SEQ-001 同类问题）
        var tenantActive = (await db.Database
                .SqlQuery<TenantRow>(
                    $"""SELECT id AS "Id", status AS "Status" FROM tenants WHERE id = {stored.TenantId}""")
                .ToListAsync())
            .SingleOrDefault() is { Status: 0 }; // TenantStatus.Active
        if (!tenantActive)
        {
            throw new AuthenticationFailedException("无效的刷新令牌");
        }

        // 一次性轮换：撤销旧令牌，签发新令牌（重放已撤销令牌 → 401）
        stored.Revoke(DateTime.UtcNow);
        var (accessToken, expiresIn) = jwtTokenIssuer.IssueAccessToken(user.TenantId, user.Id, user.Username);
        var newRefreshToken = await IssueRefreshTokenAsync(user.TenantId, user.Id);
        await db.SaveChangesAsync();

        return new RefreshResult(accessToken, expiresIn, newRefreshToken);
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var stored = await db.RefreshTokens.FindAsync([HashToken(refreshToken)]);
        if (stored is null)
        {
            return; // 幂等：不存在的令牌视为已退出
        }

        stored.Revoke(DateTime.UtcNow);
        await db.SaveChangesAsync();
    }

    public async Task<(Guid UserId, string Username, string DisplayName, Guid TenantId, IReadOnlyList<string> Permissions)>
        GetCurrentUserAsync(Guid userId)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId);
        if (user is null || user.Status != UserStatus.Active)
        {
            throw new AuthenticationFailedException("无效的用户状态");
        }

        var permissions = (await authorizationService.GetPermissionCodesAsync(user.Id)).ToList();
        return (user.Id, user.Username, user.DisplayName, user.TenantId, permissions);
    }

    // ---------- 刷新令牌 ----------

    /// <summary>签发并持久化新刷新令牌（明文仅返回客户端，落库为 SHA-256 哈希）。</summary>
    private async Task<string> IssueRefreshTokenAsync(Guid tenantId, Guid userId)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var expires = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays);

        db.RefreshTokens.Add(RefreshToken.Create(
            tenantId, userId, HashToken(token), DateTime.UtcNow, expires));
        await db.SaveChangesAsync();
        return token;
    }

    private static string HashToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private sealed record TenantRow(Guid Id, short Status);
}
