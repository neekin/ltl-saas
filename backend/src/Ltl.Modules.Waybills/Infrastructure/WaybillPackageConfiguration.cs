using Ltl.Modules.Waybills.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Waybills.Infrastructure;

/// <summary>waybill_packages 表映射（WB-001 设计文档第 4 节）。复合外键经 Waybill 聚合导航生成。</summary>
public sealed class WaybillPackageConfiguration : IEntityTypeConfiguration<WaybillPackage>
{
    public void Configure(EntityTypeBuilder<WaybillPackage> builder)
    {
        builder.ToTable("waybill_packages", table =>
        {
            table.HasCheckConstraint("ck_waybill_packages_no", "package_no >= 1");
            table.HasCheckConstraint("ck_waybill_packages_quantity", "quantity >= 1");
            table.HasCheckConstraint("ck_waybill_packages_weight", "weight_kg >= 0");
            table.HasCheckConstraint("ck_waybill_packages_volume", "volume_m3 >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.WaybillId).IsRequired();
        builder.Property(x => x.PackageNo).IsRequired();
        builder.Property(x => x.CargoName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Quantity).IsRequired();
        builder.Property(x => x.WeightKg).HasColumnType("numeric(12,3)").IsRequired();
        builder.Property(x => x.VolumeM3).HasColumnType("numeric(12,4)").IsRequired();
        builder.Property(x => x.Remark).HasMaxLength(500);

        builder.HasIndex(x => new { x.TenantId, x.WaybillId, x.PackageNo })
            .IsUnique()
            .HasDatabaseName("ux_waybill_packages_waybill_no");

        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
