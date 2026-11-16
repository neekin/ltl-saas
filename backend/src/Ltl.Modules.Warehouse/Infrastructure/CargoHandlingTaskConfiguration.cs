using Ltl.Modules.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Warehouse.Infrastructure;

/// <summary>cargo_handling_tasks 表映射（WAREHOUSE-001 设计 §3）。复合外键在迁移中显式添加。</summary>
public sealed class CargoHandlingTaskConfiguration : IEntityTypeConfiguration<CargoHandlingTask>
{
    public void Configure(EntityTypeBuilder<CargoHandlingTask> builder)
    {
        builder.ToTable("cargo_handling_tasks", table =>
        {
            table.HasCheckConstraint("ck_cargo_handling_tasks_type", "task_type IN (1)");
            table.HasCheckConstraint("ck_cargo_handling_tasks_status", "status IN (1, 2)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.WaybillId).IsRequired();
        builder.Property(x => x.BranchId).IsRequired();

        builder.Property(x => x.TaskType)
            .HasConversion(v => (short)v, v => (CargoHandlingTaskType)v)
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion(v => (short)v, v => (CargoHandlingTaskStatus)v)
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(x => x.CompletedAt).HasColumnType("timestamptz");
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();

        // 每运单同时最多一个未完成任务
        builder.HasIndex(x => new { x.TenantId, x.WaybillId })
            .IsUnique()
            .HasFilter("status = 1")
            .HasDatabaseName("ux_cargo_handling_tasks_active");

        builder.HasIndex(x => new { x.TenantId, x.WaybillId, x.CreatedAt })
            .HasDatabaseName("ix_cargo_handling_tasks_waybill");
    }
}
