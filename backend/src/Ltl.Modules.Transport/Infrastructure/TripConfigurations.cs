using Ltl.Modules.Transport.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Transport.Infrastructure;

/// <summary>transport_trips 表映射（TRIP-001 设计 §3）。跨模块复合外键在迁移中显式添加。</summary>
public sealed class TransportTripConfiguration : IEntityTypeConfiguration<TransportTrip>
{
    public void Configure(EntityTypeBuilder<TransportTrip> builder)
    {
        builder.ToTable("transport_trips", table =>
        {
            table.HasCheckConstraint("ck_transport_trips_status", "status IN (1, 2, 3)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TripNo).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status)
            .HasConversion(v => (short)v, v => (TransportTripStatus)v)
            .HasColumnType("smallint")
            .IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");

        builder.HasIndex(x => new { x.TenantId, x.TripNo })
            .IsUnique()
            .HasDatabaseName("ux_transport_trips_tenant_no");

        builder.HasIndex(x => new { x.TenantId, x.Id })
            .IsUnique()
            .HasDatabaseName("ux_transport_trips_tenant_id");

        builder.Property<uint>("xmin").IsRowVersion();
    }
}

/// <summary>transport_trip_segments 表映射。</summary>
public sealed class TransportTripSegmentConfiguration : IEntityTypeConfiguration<TransportTripSegment>
{
    public void Configure(EntityTypeBuilder<TransportTripSegment> builder)
    {
        builder.ToTable("transport_trip_segments", table =>
        {
            table.HasCheckConstraint("ck_transport_trip_segments_seq", "seq >= 1");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TripId).IsRequired();
        builder.Property(x => x.Seq).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.TripId, x.Seq })
            .IsUnique()
            .HasDatabaseName("ux_transport_trip_segments_trip_seq");
    }
}

/// <summary>transport_trip_waybills 表映射（配载行，防重复装车部分唯一索引）。</summary>
public sealed class TransportTripWaybillConfiguration : IEntityTypeConfiguration<TransportTripWaybill>
{
    public void Configure(EntityTypeBuilder<TransportTripWaybill> builder)
    {
        builder.ToTable("transport_trip_waybills");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TripId).IsRequired();
        builder.Property(x => x.WaybillId).IsRequired();
        builder.Property(x => x.LoadedAt).HasColumnType("timestamptz").IsRequired();

        // 防重复装车：同一运单不能同时被分配到两个有效批次
        builder.HasIndex(x => new { x.TenantId, x.WaybillId })
            .IsUnique()
            .HasFilter("active = true")
            .HasDatabaseName("ux_transport_trip_waybills_active_waybill");

        // 同批次不重复配载同一运单
        builder.HasIndex(x => new { x.TenantId, x.TripId, x.WaybillId })
            .IsUnique()
            .HasDatabaseName("ux_transport_trip_waybills_trip_waybill");
    }
}


/// <summary>transport_trip_fees 表映射（TRIP-002，追加式）。复合外键在迁移中显式添加。</summary>
public sealed class TransportTripFeeConfiguration : IEntityTypeConfiguration<TransportTripFee>
{
    public void Configure(EntityTypeBuilder<TransportTripFee> builder)
    {
        builder.ToTable("transport_trip_fees", table =>
        {
            table.HasCheckConstraint("ck_transport_trip_fees_amount", "amount >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TripId).IsRequired();
        builder.Property(x => x.FeeType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.OccurredAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.TripId })
            .HasDatabaseName("ix_transport_trip_fees_trip");
    }
}

/// <summary>transport_trip_events 表映射（TRIP-002，追加式）。</summary>
public sealed class TransportTripEventConfiguration : IEntityTypeConfiguration<TransportTripEvent>
{
    public void Configure(EntityTypeBuilder<TransportTripEvent> builder)
    {
        builder.ToTable("transport_trip_events");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TripId).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Detail).HasColumnType("jsonb");
        builder.Property(x => x.OccurredAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.TripId, x.OccurredAt })
            .HasDatabaseName("ix_transport_trip_events_trip");
    }
}
