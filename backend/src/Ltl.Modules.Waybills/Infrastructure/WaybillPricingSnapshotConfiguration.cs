using Ltl.Modules.Waybills.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Waybills.Infrastructure;

/// <summary>waybill_pricing_snapshots 表映射（WB-002 设计 §3）。追加式快照，不更新不删除。</summary>
public sealed class WaybillPricingSnapshotConfiguration : IEntityTypeConfiguration<WaybillPricingSnapshot>
{
    public void Configure(EntityTypeBuilder<WaybillPricingSnapshot> builder)
    {
        builder.ToTable("waybill_pricing_snapshots");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.WaybillId).IsRequired();
        builder.Property(x => x.RuleSetId).IsRequired();
        builder.Property(x => x.RuleSetCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.VersionId).IsRequired();
        builder.Property(x => x.VersionNumber).IsRequired();
        builder.Property(x => x.InputJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.QuoteJson).HasColumnType("jsonb").IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.WaybillId })
            .HasDatabaseName("ix_waybill_pricing_snapshots_waybill");

        // 同模块复合外键（模型声明，迁移自动生成）
        builder.HasOne<Waybill>()
            .WithMany()
            .HasForeignKey(s => new { s.TenantId, s.WaybillId })
            .HasPrincipalKey(w => new { w.TenantId, w.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Ignore(x => x.UpdatedAt);
        builder.Ignore(x => x.UpdatedBy);
    }
}
