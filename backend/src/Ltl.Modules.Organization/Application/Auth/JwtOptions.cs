namespace Ltl.Modules.Organization.Application.Auth;

/// <summary>
/// JWT 配置（AUTH-001）。Secret 仅来自环境变量/安全配置管理（AGENTS §11）；
/// 开发占位密钥不含真实凭据。签发契约遵守 ADR-0005（claim tenant_id）。
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 签名密钥（≥32 字符）。</summary>
    public string Secret { get; set; } = string.Empty;

    public string Issuer { get; set; } = "ltl-api";

    public string Audience { get; set; } = "ltl-clients";

    /// <summary>Access Token 有效期（分钟）。</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Refresh Token 有效期（天）。</summary>
    public int RefreshTokenDays { get; set; } = 7;
}
