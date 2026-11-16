using Ltl.Modules.Waybills.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Waybills.Infrastructure;

/// <summary>waybills 表映射（WB-001 设计文档第 4 节）。复合外键在迁移中显式添加。</summary>
public sealed class WaybillConfiguration : IEntityTypeConfiguration<Waybill>
{
    public void Configure(EntityTypeBuilder<Waybill> builder)
    {
        builder.ToTable("waybills", table =>
        {
            table.HasCheckConstraint("ck_waybills_payment_type", "payment_type IN (1, 2, 3)");
            table.HasCheckConstraint("ck_waybills_status", "waybill_status IN (1, 2, 3)");
            table.HasCheckConstraint("ck_waybills_cargo_status", "cargo_status IN (1, 2, 3)");
            table.HasCheckConstraint("ck_waybills_transport_status", "transport_status IN (1, 2)");
            table.HasCheckConstraint("ck_waybills_delivery_status", "delivery_status IN (1)");
            table.HasCheckConstraint("ck_waybills_freight_status", "freight_status IN (1, 2, 3)");
            table.HasCheckConstraint("ck_waybills_cod_status", "cod_status IN (1, 2, 3, 4, 5)");
            table.HasCheckConstraint("ck_waybills_cod_amount", "cod_amount IS NULL OR cod_amount >= 0");
            table.HasCheckConstraint("ck_waybills_total_amount", "total_amount IS NULL OR total_amount >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.WaybillNo).HasMaxLength(64).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.WaybillNo })
            .IsUnique()
            .HasDatabaseName("ux_waybills_tenant_no");

        // 复合外键目标（packages 等引用）
        builder.HasIndex(x => new { x.TenantId, x.Id })
            .IsUnique()
            .HasDatabaseName("ux_waybills_tenant_id");

        builder.Property(x => x.PaymentType)
            .HasConversion(v => (short)v, v => (WaybillPaymentType)v)
            .HasColumnType("smallint")
            .HasColumnName("payment_type")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion(v => (short)v, v => (WaybillStatus)v)
            .HasColumnType("smallint")
            .HasColumnName("waybill_status")
            .IsRequired();

        builder.Property(x => x.CargoStatus)
            .HasConversion(v => (short)v, v => (WaybillCargoStatus)v)
            .HasColumnType("smallint")
            .HasColumnName("cargo_status")
            .IsRequired();

        builder.Property(x => x.TransportStatus)
            .HasConversion(v => (short)v, v => (WaybillTransportStatus)v)
            .HasColumnType("smallint")
            .HasColumnName("transport_status")
            .IsRequired();

        builder.Property(x => x.DeliveryStatus)
            .HasConversion(v => (short)v, v => (WaybillDeliveryStatus)v)
            .HasColumnType("smallint")
            .HasColumnName("delivery_status")
            .IsRequired();

        builder.Property(x => x.FreightStatus)
            .HasConversion(v => (short)v, v => (WaybillFreightStatus)v)
            .HasColumnType("smallint")
            .HasColumnName("freight_status")
            .IsRequired();

        builder.Property(x => x.CodStatus)
            .HasConversion(v => (short)v, v => (WaybillCodStatus)v)
            .HasColumnType("smallint")
            .HasColumnName("cod_status")
            .IsRequired();

        builder.Property(x => x.CodAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.TotalAmount).HasColumnType("numeric(18,2)");

        builder.Property(x => x.SenderName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SenderPhone).HasMaxLength(50).IsRequired();
        builder.Property(x => x.SenderAddress).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ReceiverName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ReceiverPhone).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ReceiverAddress).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Remark).HasMaxLength(500);

        // 开单幂等键（WB-002）：同租户唯一，未携带（NULL）不参与唯一性
        builder.Property(x => x.IdempotencyKey);
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("idempotency_key IS NOT NULL")
            .HasDatabaseName("ux_waybills_tenant_idempotency");

        // 货物件聚合明细：绑定到私有字段 _packages
        builder.HasMany(x => x.Packages)
            .WithOne()
            .HasForeignKey(p => new { p.TenantId, p.WaybillId })
            .HasPrincipalKey(w => new { w.TenantId, w.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Packages).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
