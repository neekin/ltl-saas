using Ltl.Modules.Finance.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Finance.Infrastructure;

public sealed class ReceivableConfiguration : IEntityTypeConfiguration<Receivable>
{
    public void Configure(EntityTypeBuilder<Receivable> builder)
    {
        builder.ToTable("receivables", t => {
            t.HasCheckConstraint("ck_receivables_amount", "amount > 0");
            t.HasCheckConstraint("ck_receivables_settled", "settled_amount >= 0 AND settled_amount <= amount");
            t.HasCheckConstraint("ck_receivables_status", "status IN (1, 2, 3)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.WaybillId).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.SettledAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Status).HasConversion(v => (short)v, v => (ReceivableStatus)v).HasColumnType("smallint").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
        builder.HasIndex(x => new { x.TenantId, x.WaybillId }).IsUnique().HasDatabaseName("ux_receivables_tenant_waybill");
        builder.HasIndex(x => new { x.TenantId, x.Id }).IsUnique().HasDatabaseName("ux_receivables_tenant_id");
        builder.Property<uint>("xmin").IsRowVersion();
    }
}

public sealed class PaymentRecordConfiguration : IEntityTypeConfiguration<PaymentRecord>
{
    public void Configure(EntityTypeBuilder<PaymentRecord> builder)
    {
        builder.ToTable("payment_records", t => {
            t.HasCheckConstraint("ck_payment_records_amount", "amount > 0");
            t.HasCheckConstraint("ck_payment_records_method", "method IN (1, 2, 3)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.WaybillId).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Method).HasConversion(v => (short)v, v => (PaymentMethod)v).HasColumnType("smallint").IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.OccurredAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.WaybillId }).HasDatabaseName("ix_payment_records_waybill");
        builder.Property<uint>("xmin").IsRowVersion();
    }
}

public sealed class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> builder)
    {
        builder.ToTable("payment_allocations", t => t.HasCheckConstraint("ck_payment_allocations_amount", "amount > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PaymentId).IsRequired();
        builder.Property(x => x.ReceivableId).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.PaymentId, x.ReceivableId }).IsUnique()
            .HasDatabaseName("ux_payment_allocations_payment_receivable");
    }
}

public sealed class RefundRecordConfiguration : IEntityTypeConfiguration<RefundRecord>
{
    public void Configure(EntityTypeBuilder<RefundRecord> builder)
    {
        builder.ToTable("refund_records", t => t.HasCheckConstraint("ck_refund_records_amount", "amount > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PaymentId).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.OccurredAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.PaymentId }).HasDatabaseName("ix_refund_records_payment");
        builder.Property<uint>("xmin").IsRowVersion();
    }
}


public sealed class CodOrderConfiguration : IEntityTypeConfiguration<CodOrder>
{
    public void Configure(EntityTypeBuilder<CodOrder> builder)
    {
        builder.ToTable("cod_orders", t => {
            t.HasCheckConstraint("ck_cod_orders_amount", "amount > 0");
            t.HasCheckConstraint("ck_cod_orders_collected", "collected_amount >= 0 AND collected_amount <= amount");
            t.HasCheckConstraint("ck_cod_orders_settled", "settled_amount >= 0 AND settled_amount <= collected_amount");
            t.HasCheckConstraint("ck_cod_orders_paid", "paid_amount >= 0 AND paid_amount <= settled_amount");
            t.HasCheckConstraint("ck_cod_orders_status", "status IN (1, 2, 3, 4)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.WaybillId).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.CollectedAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.SettledAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.PaidAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Status).HasConversion(v => (short)v, v => (CodOrderStatus)v).HasColumnType("smallint").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
        builder.HasIndex(x => new { x.TenantId, x.WaybillId }).IsUnique().HasDatabaseName("ux_cod_orders_tenant_waybill");
        builder.HasIndex(x => new { x.TenantId, x.Id }).IsUnique().HasDatabaseName("ux_cod_orders_tenant_id");
        builder.Property<uint>("xmin").IsRowVersion();
    }
}

public sealed class CodCollectionConfiguration : IEntityTypeConfiguration<CodCollection>
{
    public void Configure(EntityTypeBuilder<CodCollection> builder)
    {
        builder.ToTable("cod_collections", t => t.HasCheckConstraint("ck_cod_collections_amount", "amount > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CodOrderId).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.OccurredAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.CodOrderId }).HasDatabaseName("ix_cod_collections_order");
    }
}

public sealed class CodSettlementConfiguration : IEntityTypeConfiguration<CodSettlement>
{
    public void Configure(EntityTypeBuilder<CodSettlement> builder)
    {
        builder.ToTable("cod_settlements", t => {
            t.HasCheckConstraint("ck_cod_settlements_amount", "amount > 0");
            t.HasCheckConstraint("ck_cod_settlements_status", "status IN (1, 2, 3)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CodOrderId).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Status).HasConversion(v => (short)v, v => (CodSettlementStatus)v).HasColumnType("smallint").IsRequired();
        builder.Property(x => x.ApprovedAt).HasColumnType("timestamptz");
        builder.Property(x => x.PaidAt).HasColumnType("timestamptz");
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.CodOrderId }).HasDatabaseName("ix_cod_settlements_order");
        builder.Property<uint>("xmin").IsRowVersion();
    }
}


public sealed class CustomerStatementConfiguration : IEntityTypeConfiguration<CustomerStatement>
{
    public void Configure(EntityTypeBuilder<CustomerStatement> builder)
    {
        builder.ToTable("customer_statements", t => {
            t.HasCheckConstraint("ck_customer_statements_amount", "total_amount > 0");
            t.HasCheckConstraint("ck_customer_statements_status", "status IN (1, 2, 3)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CustomerId).IsRequired();
        builder.Property(x => x.Period).HasMaxLength(6).IsRequired();
        builder.Property(x => x.TotalAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Status).HasConversion(v => (short)v, v => (CustomerStatementStatus)v).HasColumnType("smallint").IsRequired();
        builder.Property(x => x.ConfirmedAt).HasColumnType("timestamptz");
        builder.Property(x => x.ClosedAt).HasColumnType("timestamptz");
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.CustomerId, x.Period }).IsUnique()
            .HasDatabaseName("ux_customer_statements_customer_period");
        builder.HasIndex(x => new { x.TenantId, x.Id }).IsUnique().HasDatabaseName("ux_customer_statements_tenant_id");
        builder.Property<uint>("xmin").IsRowVersion();
    }
}

public sealed class CodRefundRecordConfiguration : IEntityTypeConfiguration<CodRefundRecord>
{
    public void Configure(EntityTypeBuilder<CodRefundRecord> builder)
    {
        builder.ToTable("cod_refund_records", t => t.HasCheckConstraint("ck_cod_refund_records_amount", "amount > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CollectionId).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.OccurredAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.CollectionId }).HasDatabaseName("ix_cod_refund_records_collection");
    }
}


public sealed class CustomerStatementItemConfiguration : IEntityTypeConfiguration<CustomerStatementItem>
{
    public void Configure(EntityTypeBuilder<CustomerStatementItem> builder)
    {
        builder.ToTable("customer_statement_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StatementId).IsRequired();
        builder.Property(x => x.ReceivableId).IsRequired();
        builder.Property(x => x.WaybillId).IsRequired();
        builder.Property(x => x.SnapshotAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.SnapshotSettledAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.StatementId }).HasDatabaseName("ix_customer_statement_items_statement");
    }
}
