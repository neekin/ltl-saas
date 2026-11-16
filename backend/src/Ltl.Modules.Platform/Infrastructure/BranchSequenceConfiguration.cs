using Ltl.Modules.Platform.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Platform.Infrastructure;

/// <summary>branch_sequences 表映射（SEQ-001 设计文档第 3 节）。复合外键在迁移中显式添加。</summary>
public sealed class BranchSequenceConfiguration : IEntityTypeConfiguration<BranchSequence>
{
    public void Configure(EntityTypeBuilder<BranchSequence> builder)
    {
        builder.ToTable("branch_sequences", table =>
        {
            table.HasCheckConstraint("ck_branch_sequences_current_value", "current_value >= 0");
        });

        // 复合主键：租户 + 网点 + 单据类型 + 期间
        builder.HasKey(x => new { x.TenantId, x.BranchId, x.DocType, x.Period });

        builder.Property(x => x.Period).HasMaxLength(8).IsRequired();

        builder.Property(x => x.DocType)
            .HasConversion(v => (short)v, v => (BusinessDocumentType)v)
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(x => x.CurrentValue).IsRequired();

        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz").IsRequired();
    }
}
