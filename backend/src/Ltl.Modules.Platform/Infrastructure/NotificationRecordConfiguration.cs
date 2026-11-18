using Ltl.Modules.Platform.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Platform.Infrastructure;

/// <summary>通知记录映射（NOTIFY-001）：追加式记录，租户隔离 + 事件/状态索引。</summary>
public sealed class NotificationRecordConfiguration : IEntityTypeConfiguration<NotificationRecord>
{
    public void Configure(EntityTypeBuilder<NotificationRecord> builder)
    {
        builder.ToTable("notification_records", t => {
            t.HasCheckConstraint("ck_notification_records_channel", "channel IN (1, 2, 3)");
            t.HasCheckConstraint("ck_notification_records_status", "status IN (1, 2, 3)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Recipient).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Content).HasMaxLength(500).IsRequired();
        builder.Property(x => x.FailureReason).HasMaxLength(500);
        builder.Property(x => x.Channel).HasConversion(v => (short)v, v => (NotificationChannel)v).HasColumnType("smallint").IsRequired();
        builder.Property(x => x.Status).HasConversion(v => (short)v, v => (NotificationStatus)v).HasColumnType("smallint").IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.EventType }).HasDatabaseName("ix_notification_records_event");
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt }).HasDatabaseName("ix_notification_records_created");
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
