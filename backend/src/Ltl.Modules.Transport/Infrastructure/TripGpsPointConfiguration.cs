using Ltl.Modules.Transport.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Transport.Infrastructure;

/// <summary>批次 GPS 轨迹点映射（GPS-001）：追加式，批次 + 时间索引。</summary>
public sealed class TripGpsPointConfiguration : IEntityTypeConfiguration<TripGpsPoint>
{
    public void Configure(EntityTypeBuilder<TripGpsPoint> builder)
    {
        builder.ToTable("trip_gps_points");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Latitude).HasColumnType("numeric(9,6)").IsRequired();
        builder.Property(x => x.Longitude).HasColumnType("numeric(9,6)").IsRequired();
        builder.Property(x => x.AccuracyMeters).HasColumnType("numeric(10,2)");
        builder.Property(x => x.RecordedAt).HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.TripId, x.RecordedAt }).HasDatabaseName("ix_trip_gps_points_trip_time");
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
