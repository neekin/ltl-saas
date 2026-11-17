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
