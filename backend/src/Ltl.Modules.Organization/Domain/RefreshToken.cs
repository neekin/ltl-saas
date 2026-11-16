namespace Ltl.Modules.Organization.Domain;

/// <summary>
/// 刷新令牌（AUTH-001）：仅存 SHA-256 哈希（明文不落库，AGENTS §11）；一次性轮换——
/// 刷新时撤销旧令牌并签发新令牌；重放已撤销令牌被拒绝。
/// </summary>
public sealed class RefreshToken
{
    /// <summary>哈希后的令牌（SHA-256 hex，64 字符）。</summary>
    public string TokenHash { get; private set; } = null!;

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    public DateTime? RevokedAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private RefreshToken()
    {
    }

    /// <param name="tokenHash">调用方已哈希的令牌（SHA-256 hex）。</param>
    public static RefreshToken Create(
        Guid tenantId, Guid userId, string tokenHash, DateTime createdAtUtc, DateTime expiresAtUtc)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            throw new ArgumentException("租户/用户 Id 不能为空 Guid。");
        }

        if (tokenHash.Length != 64 || tokenHash.Any(c => !Uri.IsHexDigit(c)))
        {
            throw new ArgumentException("刷新令牌必须为 64 位十六进制 SHA-256 哈希。", nameof(tokenHash));
        }

        var created = createdAtUtc.Kind == DateTimeKind.Utc
            ? createdAtUtc
            : throw new ArgumentException("时间必须为 UTC。", nameof(createdAtUtc));
        var expires = expiresAtUtc.Kind == DateTimeKind.Utc
            ? expiresAtUtc
            : throw new ArgumentException("时间必须为 UTC。", nameof(expiresAtUtc));

        if (expires <= created)
        {
            throw new ArgumentException("刷新令牌过期时间必须晚于签发时间。", nameof(expiresAtUtc));
        }

        return new RefreshToken
        {
            TokenHash = tokenHash,
            TenantId = tenantId,
            UserId = userId,
            CreatedAt = created,
            ExpiresAt = expires,
        };
    }

    /// <summary>撤销（幂等）。</summary>
    public void Revoke(DateTime nowUtc)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = nowUtc.Kind == DateTimeKind.Utc
            ? nowUtc
            : throw new ArgumentException("时间必须为 UTC。", nameof(nowUtc));
    }

    /// <summary>当前是否可用于刷新（未撤销、未过期）。</summary>
    public bool IsActive(DateTime nowUtc) => RevokedAt is null && ExpiresAt > nowUtc;
}
