using Ltl.Modules.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Warehouse.Infrastructure;

/// <summary>cargo_receipts 表映射（RECEIVE-001 设计 §3）。复合外键在迁移中显式添加。</summary>
public sealed class CargoReceiptConfiguration : IEntityTypeConfiguration<CargoReceipt>
{
    public void Configure(EntityTypeBuilder<CargoReceipt> builder)
    {
        builder.ToTable("cargo_receipts", table =>
        {
            table.HasCheckConstraint("ck_cargo_receipts_pieces", "received_pieces >= 1");
            table.HasCheckConstraint("ck_cargo_receipts_weight", "received_weight_kg >= 0");
            table.HasCheckConstraint("ck_cargo_receipts_volume", "received_volume_m3 >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.WaybillId).IsRequired();
        builder.Property(x => x.BranchId).IsRequired();
        builder.Property(x => x.ReceivedPieces).IsRequired();
        builder.Property(x => x.ReceivedWeightKg).HasColumnType("numeric(12,3)").IsRequired();
        builder.Property(x => x.ReceivedVolumeM3).HasColumnType("numeric(12,4)").IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.OccurredAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.WaybillId, x.OccurredAt })
            .HasDatabaseName("ix_cargo_receipts_waybill");
    }
}
