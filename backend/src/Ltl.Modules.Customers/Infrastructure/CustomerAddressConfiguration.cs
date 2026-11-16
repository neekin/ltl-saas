using Ltl.Modules.Customers.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Customers.Infrastructure;

/// <summary>customer_addresses 表映射。默认地址唯一性由部分唯一索引在数据库层保证。</summary>
public sealed class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> builder)
    {
        builder.ToTable("customer_addresses");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Label).HasMaxLength(100);
        builder.Property(x => x.Address).HasMaxLength(500).IsRequired();
        builder.Property(x => x.IsDefault).IsRequired();
        builder.Property(x => x.Remark).HasMaxLength(500);

        builder.HasIndex(x => new { x.TenantId, x.CustomerId })
            .HasFilter("is_default")
            .IsUnique()
            .HasDatabaseName("ux_customer_addresses_tenant_customer_default");

        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
