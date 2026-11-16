using Ltl.Modules.Tenancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Tenancy.Infrastructure;

/// <summary>tenants 表映射（docs/requirements/TEN-001-tenant-context.md 第 3 节）。</summary>
public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants", table =>
        {
            table.HasCheckConstraint("ck_tenants_status", "status IN (0, 1)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("ux_tenants_code");

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion(v => (short)v, v => (TenantStatus)v)
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
        builder.Property(x => x.UpdatedBy);

        // PostgreSQL xmin 系统列作为乐观并发令牌（AGENTS.md §8：并发更新用乐观并发控制）
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
