using Ltl.Modules.Customers.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Customers.Infrastructure;

/// <summary>customers 表映射（CUS-001 设计文档第 3 节）。复合外键在迁移中显式添加。</summary>
public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers", table =>
        {
            table.HasCheckConstraint("ck_customers_status", "status IN (0, 1)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Remark).HasMaxLength(500);
        builder.Property(x => x.CreditLimit).HasColumnType("numeric(18,2)");
        builder.Property(x => x.PaymentTermsDays);

        builder.HasIndex(x => new { x.TenantId, x.Code })
            .IsUnique()
            .HasDatabaseName("ux_customers_tenant_code");

        // 复合外键目标唯一索引：contacts/addresses/branch_relations (tenant_id, customer_id) → customers
        builder.HasIndex(x => new { x.TenantId, x.Id })
            .IsUnique()
            .HasDatabaseName("ux_customers_tenant_id");

        builder.Property(x => x.Status)
            .HasConversion(v => (short)v, v => (CustomerStatus)v)
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
