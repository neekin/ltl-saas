using Ltl.Modules.Transport.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Transport.Infrastructure;

/// <summary>drivers 表映射（VEHICLE-001）。tenants 外键在迁移中显式添加。</summary>
public sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("drivers", table =>
        {
            table.HasCheckConstraint("ck_drivers_status", "status IN (0, 1)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(50).IsRequired();
        builder.Property(x => x.LicenseNo).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");

        builder.HasIndex(x => new { x.TenantId, x.LicenseNo })
            .IsUnique()
            .HasDatabaseName("ux_drivers_tenant_license");

        builder.HasIndex(x => new { x.TenantId, x.Id })
            .IsUnique()
            .HasDatabaseName("ux_drivers_tenant_id");

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
