using Ltl.Modules.Platform.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Platform.Infrastructure;

/// <summary>file_objects 表映射（FILE-001 设计 §3）。tenants 外键在迁移中显式添加。</summary>
public sealed class FileObjectConfiguration : IEntityTypeConfiguration<FileObject>
{
    public void Configure(EntityTypeBuilder<FileObject> builder)
    {
        builder.ToTable("file_objects", table =>
        {
            table.HasCheckConstraint("ck_file_objects_size", "size_bytes > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SizeBytes).IsRequired();
        builder.Property(x => x.StorageProvider).HasMaxLength(20).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasIndex(x => x.StorageKey)
            .IsUnique()
            .HasDatabaseName("ux_file_objects_storage_key");
    }
}
