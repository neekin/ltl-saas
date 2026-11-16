using Ltl.Modules.Organization.Domain;
using Ltl.SharedKernel.Security;
using Xunit;

namespace Ltl.UnitTests.Organization;

public class UserTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private const string PasswordHash = "pbkdf2-sha256$100000$abcdefghij1234567890==$c29tZWhhc2g=";

    [Fact]
    public void Create_ShouldNormalizeAndSetActive()
    {
        var user = User.Create(TenantId, " zhangsan ", " 张三 ", PasswordHash, DateTime.UtcNow);

        Assert.Equal("zhangsan", user.Username);
        Assert.Equal("张三", user.DisplayName);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.False(user.TenantWideScope);
        Assert.Equal(TenantId, user.TenantId);
    }

    [Fact]
    public void Create_WithTenantWideScope_ShouldExposeFlag()
    {
        var user = User.Create(TenantId, "zhangsan", "张三", PasswordHash, DateTime.UtcNow, tenantWideScope: true);

        Assert.True(user.TenantWideScope);
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            () => User.Create(Guid.Empty, "zhangsan", "张三", PasswordHash, DateTime.UtcNow));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankPasswordHash_ShouldThrow(string? passwordHash)
    {
        Assert.Throws<ArgumentException>(
            () => User.Create(TenantId, "zhangsan", "张三", passwordHash!, DateTime.UtcNow));
    }

    [Fact]
    public void ResetPassword_ShouldReplaceHashAndTouch()
    {
        var user = User.Create(TenantId, "zhangsan", "张三", PasswordHash, DateTime.UtcNow);
        var newHash = PasswordHasher.Hash("new-password-123");
        var now = DateTime.UtcNow;

        user.ResetPassword(newHash, now);

        Assert.Equal(newHash, user.PasswordHash);
        Assert.Equal(now, user.UpdatedAt);
    }

    [Fact]
    public void Disable_ShouldBeIdempotent()
    {
        var user = User.Create(TenantId, "zhangsan", "张三", PasswordHash, DateTime.UtcNow);
        var now = DateTime.UtcNow;

        user.Disable(now);
        user.Disable(now.AddMinutes(1));

        Assert.Equal(UserStatus.Disabled, user.Status);
        Assert.Equal(now, user.UpdatedAt);
    }
}
