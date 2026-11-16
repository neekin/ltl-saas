namespace Ltl.Modules.Organization.Application.Auth;

/// <summary>Access Token 签发（HS256；claim 契约见 ADR-0005/TenantClaimTypes）。</summary>
public interface IJwtTokenIssuer
{
    /// <summary>签发 Access Token；返回（token, 有效期秒）。</summary>
    (string Token, int ExpiresInSeconds) IssueAccessToken(Guid tenantId, Guid userId, string username);
}
