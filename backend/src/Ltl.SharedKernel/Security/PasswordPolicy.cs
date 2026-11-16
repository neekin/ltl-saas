namespace Ltl.SharedKernel.Security;

/// <summary>
/// 密码策略（AUTH-001，第一版建议值，集中封装可调）：
/// 长度 8..64，至少包含字母与数字，拒绝空白字符。应用于用户创建/改密；登录验证由 PasswordHasher 承担。
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 64;

    /// <summary>校验密码；不合法时返回错误说明，合法返回 null。</summary>
    public static string? Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return "密码不能为空。";
        }

        if (password.Length < MinLength)
        {
            return $"密码长度不能少于 {MinLength} 位。";
        }

        if (password.Length > MaxLength)
        {
            return $"密码长度不能超过 {MaxLength} 位。";
        }

        if (password.Any(char.IsWhiteSpace))
        {
            return "密码不能包含空白字符。";
        }

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            return "密码必须同时包含字母和数字。";
        }

        return null;
    }
}
