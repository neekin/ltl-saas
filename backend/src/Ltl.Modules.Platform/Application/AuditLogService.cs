using System.Text.Json;
using Ltl.Modules.Platform.Domain;
using Ltl.Modules.Platform.Infrastructure;
using Ltl.SharedKernel.Application;

namespace Ltl.Modules.Platform.Application;

/// <summary>审计日志实现（追加式写入；租户来自租户上下文）。</summary>
public sealed class AuditLogService(PlatformDbContext db) : IAuditLogService
{
    private static readonly JsonSerializerOptions DetailOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task RecordAsync(
        string action, string entityType, Guid? entityId, Guid? actorUserId, object? detail)
    {
        var tenantId = db.CurrentTenantId
            ?? throw new TenantContextMissingException();

        var now = DateTime.UtcNow;
        db.AuditLogs.Add(AuditLog.Create(
            tenantId,
            action,
            entityType,
            entityId,
            actorUserId,
            detail is null ? null : JsonSerializer.Serialize(detail, DetailOptions),
            now));
        await db.SaveChangesAsync();
    }
}
