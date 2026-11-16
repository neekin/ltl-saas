using Ltl.Modules.Pricing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Pricing.Infrastructure;

/// <summary>
/// pricing_rule_set_versions 表映射（PRICE-001 设计文档第 4 节）。
/// 部分唯一索引强制"每集合最多一个 Published / 一个 Draft"；复合外键在迁移中显式添加。
/// </summary>
public sealed class PricingRuleSetVersionConfiguration : IEntityTypeConfiguration<PricingRuleSetVersion>
{
    public void Configure(EntityTypeBuilder<PricingRuleSetVersion> builder)
    {
        builder.ToTable("pricing_rule_set_versions", table =>
        {
            table.HasCheckConstraint("ck_pricing_rule_set_versions_status", "status IN (0, 1, 2)");
            table.HasCheckConstraint("ck_pricing_rule_set_versions_number", "version_number >= 1");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RuleSetId).IsRequired();
        builder.Property(x => x.VersionNumber).IsRequired();

        builder.Property(x => x.Status)
            .HasConversion(v => (short)v, v => (PricingVersionStatus)v)
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(x => x.Definition)
            .HasColumnType("jsonb")
            .HasConversion(PricingJson.DefinitionConverter)
            .IsRequired();

        builder.Property(x => x.PublishedAt).HasColumnType("timestamptz");
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");

        // 集合内版本号唯一
        builder.HasIndex(x => new { x.TenantId, x.RuleSetId, x.VersionNumber })
            .IsUnique()
            .HasDatabaseName("ux_pricing_rule_set_versions_set_version");

        // 每集合同时最多一个已发布版本（当前生效版本）。
        // 列顺序与 draft 索引相反：EF 对同属性集合的两个 HasIndex 会合并为同一索引对象（后者覆盖前者）
        builder.HasIndex(x => new { x.RuleSetId, x.TenantId })
            .IsUnique()
            .HasFilter("status = 1")
            .HasDatabaseName("ux_pricing_rule_set_versions_current_published");

        // 每集合同时最多一个草稿
        builder.HasIndex(x => new { x.TenantId, x.RuleSetId })
            .IsUnique()
            .HasFilter("status = 0")
            .HasDatabaseName("ux_pricing_rule_set_versions_draft");

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
