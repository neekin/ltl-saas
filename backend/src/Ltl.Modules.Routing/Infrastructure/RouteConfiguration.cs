using Ltl.Modules.Routing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Routing.Infrastructure;

/// <summary>routes 表映射（ROUTE-001 设计文档第 3 节）。复合外键在迁移中显式添加。</summary>
public sealed class RouteConfiguration : IEntityTypeConfiguration<Route>
{
    public void Configure(EntityTypeBuilder<Route> builder)
    {
        builder.ToTable("routes", table =>
        {
            table.HasCheckConstraint("ck_routes_status", "status IN (0, 1)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.Code })
            .IsUnique()
            .HasDatabaseName("ux_routes_tenant_code");

        builder.HasIndex(x => new { x.TenantId, x.Id })
            .IsUnique()
            .HasDatabaseName("ux_routes_tenant_id");

        builder.Property(x => x.Status)
            .HasConversion(v => (short)v, v => (RouteStatus)v)
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
