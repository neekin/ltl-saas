using Ltl.SharedKernel.Security;
using Xunit;

namespace Ltl.UnitTests.SharedKernel;

/// <summary>AUTH-001 密码策略测试（第一版建议值：8..64 位，字母+数字，无空白）。</summary>
public class PasswordPolicyTests
{
    [Theory]
    [InlineData("abc12345")] // 8 位，字母+数字
    [InlineData("Abcdef12345678901234567890123456789012345678901234567890Abcdefgh")] // 64 位
    public void ValidPasswords_ShouldPass(string password)
    {
        Assert.Null(PasswordPolicy.Validate(password));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Blank_ShouldFail(string? password)
    {
        Assert.NotNull(PasswordPolicy.Validate(password));
    }

    [Fact]
    public void TooShort_ShouldFail()
    {
        var error = PasswordPolicy.Validate("ab12345"); // 7 位
        Assert.Contains("不能少于 8", error);
    }

    [Fact]
    public void TooLong_ShouldFail()
    {
        var password = new string('a', 32) + "123456789012345678901234567890123"; // 65 位
        var error = PasswordPolicy.Validate(password);
        Assert.Contains("不能超过 64", error);
    }

    [Theory]
    [InlineData("abcdefgh")] // 仅字母
    [InlineData("12345678")] // 仅数字
    public void MissingCharacterClass_ShouldFail(string password)
    {
        Assert.Contains("字母和数字", PasswordPolicy.Validate(password));
    }

    [Fact]
    public void WithWhitespace_ShouldFail()
    {
        Assert.Contains("空白字符", PasswordPolicy.Validate("abc 12345"));
    }
}
