namespace Ltl.Modules.Organization.Application.Auth;

/// <summary>
/// 认证失败（登录/刷新/退出/当前用户）。API 任务映射为 401；
/// 登录失败统一"登录名或密码不正确"，不区分具体原因（防枚举，AUTH-001 §2）。
/// </summary>
public sealed class AuthenticationFailedException(string message) : Exception(message);
