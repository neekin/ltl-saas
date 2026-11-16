using Ltl.SharedKernel.Security;
using Xunit;

namespace Ltl.UnitTests.SharedKernel;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ThenVerify_ShouldMatch()
    {
        var hash = PasswordHasher.Hash("my-secret-123");

        Assert.True(PasswordHasher.Verify("my-secret-123", hash));
        Assert.False(PasswordHasher.Verify("wrong-password", hash));
    }

    [Fact]
    public void Hash_SamePasswordTwice_ShouldProduceDifferentSalts()
    {
        var hash1 = PasswordHasher.Hash("same-password");
        var hash2 = PasswordHasher.Hash("same-password");

        Assert.NotEqual(hash1, hash2);
        Assert.True(PasswordHasher.Verify("same-password", hash1));
        Assert.True(PasswordHasher.Verify("same-password", hash2));
    }

    [Fact]
    public void Hash_ShouldFollowStorageFormat()
    {
        var hash = PasswordHasher.Hash("my-secret-123");

        var parts = hash.Split('$');
        Assert.Equal(4, parts.Length);
        Assert.Equal("pbkdf2-sha256", parts[0]);
        Assert.Equal("100000", parts[1]);
        Assert.Equal(16, Convert.FromBase64String(parts[2]).Length);
        Assert.Equal(32, Convert.FromBase64String(parts[3]).Length);
    }

    [Fact]
    public void Verify_WithMalformedHash_ShouldReturnFalse()
    {
        Assert.False(PasswordHasher.Verify("my-secret-123", "not-a-hash"));
        Assert.False(PasswordHasher.Verify("my-secret-123", "md5$1000$aa$bb"));
        Assert.False(PasswordHasher.Verify("my-secret-123", "pbkdf2-sha256$not-int$aa$bb"));
    }

    [Fact]
    public void Hash_WithTooFewIterations_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PasswordHasher.Hash("my-secret-123", 1));
    }
}
