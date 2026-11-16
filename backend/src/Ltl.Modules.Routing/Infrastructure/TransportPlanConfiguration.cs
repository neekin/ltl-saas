using Ltl.Modules.Routing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Routing.Infrastructure;

/// <summary>transport_plans 表映射。</summary>
public sealed class TransportPlanConfiguration : IEntityTypeConfiguration<TransportPlan>
{
    public void Configure(EntityTypeBuilder<TransportPlan> builder)
    {
        builder.ToTable("transport_plans", table =>
        {
            table.HasCheckConstraint("ck_transport_plans_status", "status IN (0, 1)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.Id })
            .IsUnique()
            .HasDatabaseName("ux_transport_plans_tenant_id");

        builder.Property(x => x.Status)
            .HasConversion(v => (short)v, v => (RouteStatus)v)
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
