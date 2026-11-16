using Ltl.Modules.Routing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Routing.Infrastructure;

/// <summary>route_stops 表映射：同线路同序号/同网点唯一。</summary>
public sealed class RouteStopConfiguration : IEntityTypeConfiguration<RouteStop>
{
    public void Configure(EntityTypeBuilder<RouteStop> builder)
    {
        builder.ToTable("route_stops");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Sequence).IsRequired();
        builder.Property(x => x.Remark).HasMaxLength(200);

        builder.HasIndex(x => new { x.TenantId, x.RouteId, x.Sequence })
            .IsUnique()
            .HasDatabaseName("ux_route_stops_tenant_route_sequence");

        builder.HasIndex(x => new { x.TenantId, x.RouteId, x.BranchId })
            .IsUnique()
            .HasDatabaseName("ux_route_stops_tenant_route_branch");

        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
