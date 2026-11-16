using Ltl.Modules.Pricing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Pricing.Infrastructure;

/// <summary>pricing_rule_sets 表映射（PRICE-001 设计文档第 4 节）。复合外键在迁移中显式添加。</summary>
public sealed class PricingRuleSetConfiguration : IEntityTypeConfiguration<PricingRuleSet>
{
    public void Configure(EntityTypeBuilder<PricingRuleSet> builder)
    {
        builder.ToTable("pricing_rule_sets", table =>
        {
            table.HasCheckConstraint("ck_pricing_rule_sets_status", "status IN (1, 2)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.Code })
            .IsUnique()
            .HasDatabaseName("ux_pricing_rule_sets_tenant_code");

        // 复合外键目标（versions 的 (tenant_id, rule_set_id) 引用）
        builder.HasIndex(x => new { x.TenantId, x.Id })
            .IsUnique()
            .HasDatabaseName("ux_pricing_rule_sets_tenant_id");

        builder.Property(x => x.Status)
            .HasConversion(v => (short)v, v => (PricingRuleSetStatus)v)
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
