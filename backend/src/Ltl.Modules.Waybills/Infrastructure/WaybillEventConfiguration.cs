using Ltl.Modules.Waybills.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Waybills.Infrastructure;

/// <summary>waybill_events 表映射（WB-002 设计 §4）。追加式审计记录。</summary>
public sealed class WaybillEventConfiguration : IEntityTypeConfiguration<WaybillEvent>
{
    public void Configure(EntityTypeBuilder<WaybillEvent> builder)
    {
        builder.ToTable("waybill_events", table =>
        {
            table.HasCheckConstraint("ck_waybill_events_type", "event_type IN (1, 2, 3, 4, 5)");
            table.HasCheckConstraint(
                "ck_waybill_events_from_status", "from_status IS NULL OR from_status IN (1, 2, 3)");
            table.HasCheckConstraint("ck_waybill_events_to_status", "to_status IN (1, 2, 3)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.WaybillId).IsRequired();

        builder.Property(x => x.EventType)
            .HasConversion(v => (short)v, v => (WaybillEventType)v)
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(x => x.FromStatus)
            .HasConversion(v => (short?)v, v => (WaybillStatus?)v)
            .HasColumnType("smallint");

        builder.Property(x => x.ToStatus)
            .HasConversion(v => (short)v, v => (WaybillStatus)v)
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(x => x.OccurredAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.Detail).HasMaxLength(500);

        builder.HasIndex(x => new { x.TenantId, x.WaybillId, x.OccurredAt })
            .HasDatabaseName("ix_waybill_events_waybill_occurred");

        // 同模块复合外键（模型声明，迁移自动生成）
        builder.HasOne<Waybill>()
            .WithMany()
            .HasForeignKey(e => new { e.TenantId, e.WaybillId })
            .HasPrincipalKey(w => new { w.TenantId, w.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Ignore(x => x.UpdatedAt);
        builder.Ignore(x => x.UpdatedBy);
    }
}
