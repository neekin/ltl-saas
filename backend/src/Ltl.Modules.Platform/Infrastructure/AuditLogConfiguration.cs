using Ltl.Modules.Platform.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Platform.Infrastructure;

/// <summary>audit_logs 表映射（ORG-003 设计 §6）。追加式，tenants 外键在迁移中显式添加。</summary>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(x => new { x.TenantId, x.Id });

        builder.Property(x => x.OccurredAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.Action).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Detail).HasColumnType("jsonb");
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();

        // 时间线查询：按租户 + 时间倒序
        builder.HasIndex(x => new { x.TenantId, x.OccurredAt })
            .HasDatabaseName("ix_audit_logs_tenant_occurred");
    }
}
