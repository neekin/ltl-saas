using Ltl.Modules.Delivery.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Delivery.Infrastructure;

/// <summary>delivery_orders / POD / delivery_events 映射（DELIVERY-001）。</summary>
public sealed class DeliveryOrderConfiguration : IEntityTypeConfiguration<DeliveryOrder>
{
    public void Configure(EntityTypeBuilder<DeliveryOrder> builder)
    {
        builder.ToTable("delivery_orders", table =>
        {
            table.HasCheckConstraint("ck_delivery_orders_type", "delivery_type IN (1, 2)");
            table.HasCheckConstraint("ck_delivery_orders_status", "status IN (1, 2, 3)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.WaybillId).IsRequired();
        builder.Property(x => x.BranchId).IsRequired();
        builder.Property(x => x.DeliveryType)
            .HasConversion(v => (short)v, v => (DeliveryType)v).HasColumnType("smallint").IsRequired();
        builder.Property(x => x.Status)
            .HasConversion(v => (short)v, v => (DeliveryOrderStatus)v).HasColumnType("smallint").IsRequired();
        builder.Property(x => x.ReceiverName).HasMaxLength(100);
        builder.Property(x => x.CompletedAt).HasColumnType("timestamptz");
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.WaybillId }).IsUnique().HasFilter("status = 1")
            .HasDatabaseName("ux_delivery_orders_active_waybill");
        builder.HasIndex(x => new { x.TenantId, x.Id }).IsUnique().HasDatabaseName("ux_delivery_orders_tenant_id");
        builder.Property<uint>("xmin").IsRowVersion();
    }
}

public sealed class ProofOfDeliveryConfiguration : IEntityTypeConfiguration<ProofOfDelivery>
{
    public void Configure(EntityTypeBuilder<ProofOfDelivery> builder)
    {
        builder.ToTable("proof_of_deliveries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DeliveryOrderId).IsRequired();
        builder.Property(x => x.ReceiverName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SignedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.DeliveryOrderId }).IsUnique()
            .HasDatabaseName("ux_proof_of_deliveries_order");
    }
}

public sealed class DeliveryEventConfiguration : IEntityTypeConfiguration<DeliveryEvent>
{
    public void Configure(EntityTypeBuilder<DeliveryEvent> builder)
    {
        builder.ToTable("delivery_events");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DeliveryOrderId).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Detail).HasColumnType("jsonb");
        builder.Property(x => x.OccurredAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.DeliveryOrderId })
            .HasDatabaseName("ix_delivery_events_order");
    }
}
