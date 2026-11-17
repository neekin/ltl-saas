using Ltl.Modules.Arrivals.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Arrivals.Infrastructure;

/// <summary>arrivals / arrival_items 表映射（ARRIVAL-001）。复合外键迁移显式添加。</summary>
public sealed class CargoArrivalConfiguration : IEntityTypeConfiguration<CargoArrival>
{
    public void Configure(EntityTypeBuilder<CargoArrival> builder)
    {
        builder.ToTable("arrivals");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TripId).IsRequired();
        builder.Property(x => x.BranchId).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.ArrivedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.TripId })
            .IsUnique()
            .HasDatabaseName("ux_arrivals_tenant_trip");

        builder.HasIndex(x => new { x.TenantId, x.Id })
            .IsUnique()
            .HasDatabaseName("ux_arrivals_tenant_id");

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(i => new { i.TenantId, i.ArrivalId })
            .HasPrincipalKey(a => new { a.TenantId, a.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>arrival_items 表映射（追加式差异留痕）。</summary>
public sealed class ArrivalItemConfiguration : IEntityTypeConfiguration<ArrivalItem>
{
    public void Configure(EntityTypeBuilder<ArrivalItem> builder)
    {
        builder.ToTable("arrival_items", table =>
        {
            table.HasCheckConstraint("ck_arrival_items_loaded", "loaded_pieces >= 1");
            table.HasCheckConstraint("ck_arrival_items_received", "received_pieces >= 0");
            table.HasCheckConstraint("ck_arrival_items_broken", "broken_pieces >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ArrivalId).IsRequired();
        builder.Property(x => x.WaybillId).IsRequired();
        builder.Property(x => x.LoadedPieces).IsRequired();
        builder.Property(x => x.ReceivedPieces).IsRequired();
        builder.Property(x => x.BrokenPieces).IsRequired();
        builder.Property(x => x.Remark).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.WaybillId })
            .IsUnique()
            .HasDatabaseName("ux_arrival_items_tenant_waybill");
    }
}
