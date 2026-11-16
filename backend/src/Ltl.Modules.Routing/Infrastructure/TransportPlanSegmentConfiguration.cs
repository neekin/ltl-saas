using Ltl.Modules.Routing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Routing.Infrastructure;

/// <summary>transport_plan_segments 表映射：同方案同序号唯一。</summary>
public sealed class TransportPlanSegmentConfiguration : IEntityTypeConfiguration<TransportPlanSegment>
{
    public void Configure(EntityTypeBuilder<TransportPlanSegment> builder)
    {
        builder.ToTable("transport_plan_segments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Sequence).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.PlanId, x.Sequence })
            .IsUnique()
            .HasDatabaseName("ux_transport_plan_segments_tenant_plan_sequence");

        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
