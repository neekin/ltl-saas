using Ltl.Modules.Organization.Domain;
using Xunit;

namespace Ltl.UnitTests.Organization;

/// <summary>AUTH-001 刷新令牌实体测试：签发约束、撤销幂等、有效性判断。</summary>
public class RefreshTokenTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private const string ValidHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"; // 64 hex

    private static readonly DateTime Now = DateTime.UtcNow;

    [Fact]
    public void Create_WithValidHash_ShouldStartActive()
    {
        var token = RefreshToken.Create(TenantId, UserId, ValidHash, Now, Now.AddDays(7));

        Assert.True(token.IsActive(Now));
        Assert.Null(token.RevokedAt);
    }

    [Fact]
    public void Create_WithInvalidHash_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            () => RefreshToken.Create(TenantId, UserId, "not-a-hash", Now, Now.AddDays(7)));
        Assert.Throws<ArgumentException>(
            () => RefreshToken.Create(TenantId, UserId, new string('a', 63), Now, Now.AddDays(7)));
    }

    [Fact]
    public void Create_WithExpiryBeforeIssue_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            () => RefreshToken.Create(TenantId, UserId, ValidHash, Now, Now.AddDays(-1)));
    }

    [Fact]
    public void Revoke_ShouldBeIdempotentAndDeactivate()
    {
        var token = RefreshToken.Create(TenantId, UserId, ValidHash, Now, Now.AddDays(7));

        token.Revoke(Now.AddMinutes(1));
        token.Revoke(Now.AddMinutes(2)); // 幂等

        Assert.False(token.IsActive(Now.AddMinutes(3)));
    }

    [Fact]
    public void ExpiredToken_ShouldBeInactive()
    {
        var token = RefreshToken.Create(TenantId, UserId, ValidHash, Now, Now.AddSeconds(30));

        Assert.True(token.IsActive(Now));
        Assert.False(token.IsActive(Now.AddSeconds(60)));
    }
}
