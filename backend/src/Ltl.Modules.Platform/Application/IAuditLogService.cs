namespace Ltl.Modules.Platform.Application;

/// <summary>
/// 审计日志服务（ORG-003）：写入关键写操作的留痕。
/// 原子性约定：调用方以 TransactionScope 包裹业务保存与本服务写入（两 DbContext 同库，
/// 经环境事务 enlist）——审计失败业务回滚、业务失败无审计残留（AGENTS §4.3 同库强一致）。
/// 租户来自请求租户上下文（fail-closed）。
/// </summary>
public interface IAuditLogService
{
    /// <summary>记录一条审计日志。detail 序列化为 JSONB；不得包含敏感值（密码哈希/令牌等）。</summary>
    /// <exception cref="TenantContextMissingException">无租户上下文。</exception>
    Task RecordAsync(string action, string entityType, Guid? entityId, Guid? actorUserId, object? detail);
}
