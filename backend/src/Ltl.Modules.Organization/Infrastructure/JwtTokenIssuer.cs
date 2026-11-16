using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ltl.Modules.Organization.Application.Auth;
using Ltl.SharedKernel.Application;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Ltl.Modules.Organization.Infrastructure;

/// <summary>
/// Access Token 签发（HS256）。claims 遵守 ADR-0005：tenant_id 必须为 TenantClaimTypes.TenantId；
/// 不含权限点（授权实时查库，ORG-002 语义）。
/// </summary>
public sealed class JwtTokenIssuer : IJwtTokenIssuer
{
    private readonly JwtOptions _options;

    public JwtTokenIssuer(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        if (_options.Secret.Length < 32)
        {
            throw new InvalidOperationException("JWT 签名密钥必须 ≥32 字符（配置管理注入，禁止仓库提交真实密钥）。");
        }
    }

    public (string Token, int ExpiresInSeconds) IssueAccessToken(Guid tenantId, Guid userId, string username)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(TenantClaimTypes.TenantId, tenantId.ToString()),
            new("username", username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            _options.Issuer, _options.Audience, claims, notBefore: now, expires: expires, signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token),
            (int)(expires - now).TotalSeconds);
    }
}
