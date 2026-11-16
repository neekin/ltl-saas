using Ltl.Modules.Customers.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Customers.Infrastructure;

/// <summary>customer_contacts 表映射。主联系人唯一性由部分唯一索引在数据库层保证。</summary>
public sealed class CustomerContactConfiguration : IEntityTypeConfiguration<CustomerContact>
{
    public void Configure(EntityTypeBuilder<CustomerContact> builder)
    {
        builder.ToTable("customer_contacts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(50).IsRequired();
        builder.Property(x => x.IsPrimary).IsRequired();
        builder.Property(x => x.Remark).HasMaxLength(500);

        builder.HasIndex(x => new { x.TenantId, x.CustomerId })
            .HasFilter("is_primary")
            .IsUnique()
            .HasDatabaseName("ux_customer_contacts_tenant_customer_primary");

        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
