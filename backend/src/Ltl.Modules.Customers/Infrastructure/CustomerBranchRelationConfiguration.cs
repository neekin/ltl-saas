using Ltl.Modules.Customers.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Customers.Infrastructure;

/// <summary>customer_branch_relations 关联表映射：复合主键 (tenant_id, customer_id, branch_id)。</summary>
public sealed class CustomerBranchRelationConfiguration : IEntityTypeConfiguration<CustomerBranchRelation>
{
    public void Configure(EntityTypeBuilder<CustomerBranchRelation> builder)
    {
        builder.ToTable("customer_branch_relations");

        builder.HasKey(x => new { x.TenantId, x.CustomerId, x.BranchId });

        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
    }
}
