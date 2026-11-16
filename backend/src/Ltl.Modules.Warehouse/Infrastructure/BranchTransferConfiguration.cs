using Ltl.Modules.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Warehouse.Infrastructure;

/// <summary>branch_transfers / branch_transfer_items 表映射（TRANSFER-001）。复合外键迁移显式添加。</summary>
public sealed class BranchTransferConfiguration : IEntityTypeConfiguration<BranchTransfer>
{
    public void Configure(EntityTypeBuilder<BranchTransfer> builder)
    {
        builder.ToTable("branch_transfers", table =>
        {
            table.HasCheckConstraint("ck_branch_transfers_status",
                "status IN (1, 2, 3, 4, 5)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TransferNo).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status)
            .HasConversion(v => (short)v, v => (BranchTransferStatus)v)
            .HasColumnType("smallint")
            .IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");

        builder.HasIndex(x => new { x.TenantId, x.TransferNo })
            .IsUnique()
            .HasDatabaseName("ux_branch_transfers_tenant_no");

        builder.HasIndex(x => new { x.TenantId, x.Id })
            .IsUnique()
            .HasDatabaseName("ux_branch_transfers_tenant_id");

        builder.Property<uint>("xmin").IsRowVersion();
    }
}

/// <summary>branch_transfer_items 表映射（验收字段一次性写入）。</summary>
public sealed class BranchTransferItemConfiguration : IEntityTypeConfiguration<BranchTransferItem>
{
    public void Configure(EntityTypeBuilder<BranchTransferItem> builder)
    {
        builder.ToTable("branch_transfer_items", table =>
        {
            table.HasCheckConstraint("ck_branch_transfer_items_seq", "seq >= 1");
            table.HasCheckConstraint("ck_branch_transfer_items_declared", "declared_quantity >= 1");
            table.HasCheckConstraint("ck_branch_transfer_items_received",
                "received_quantity IS NULL OR received_quantity >= 0");
            table.HasCheckConstraint("ck_branch_transfer_items_broken",
                "broken_quantity IS NULL OR broken_quantity >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TransferId).IsRequired();
        builder.Property(x => x.Seq).IsRequired();
        builder.Property(x => x.CargoName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DeclaredQuantity).IsRequired();
        builder.Property(x => x.Remark).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.TransferId, x.Seq })
            .IsUnique()
            .HasDatabaseName("ux_branch_transfer_items_transfer_seq");
    }
}
