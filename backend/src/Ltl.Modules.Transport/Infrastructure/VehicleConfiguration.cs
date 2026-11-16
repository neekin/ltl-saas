using Ltl.Modules.Transport.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Transport.Infrastructure;

/// <summary>vehicles 表映射（VEHICLE-001）。tenants 外键在迁移中显式添加。</summary>
public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles", table =>
        {
            table.HasCheckConstraint("ck_vehicles_status", "status IN (0, 1)");
            table.HasCheckConstraint("ck_vehicles_load", "max_load_kg > 0");
            table.HasCheckConstraint("ck_vehicles_volume", "max_volume_m3 > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PlateNo).HasMaxLength(20).IsRequired();
        builder.Property(x => x.VehicleType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.MaxLoadKg).HasColumnType("numeric(12,3)").IsRequired();
        builder.Property(x => x.MaxVolumeM3).HasColumnType("numeric(12,4)").IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");

        builder.HasIndex(x => new { x.TenantId, x.PlateNo })
            .IsUnique()
            .HasDatabaseName("ux_vehicles_tenant_plate");

        builder.HasIndex(x => new { x.TenantId, x.Id })
            .IsUnique()
            .HasDatabaseName("ux_vehicles_tenant_id");

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
