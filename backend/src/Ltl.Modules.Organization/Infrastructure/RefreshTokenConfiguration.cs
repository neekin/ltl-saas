using Ltl.Modules.Organization.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Organization.Infrastructure;

/// <summary>refresh_tokens 表映射（AUTH-001 设计 §5）。租户外键在迁移中显式添加。</summary>
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens", table =>
        {
            table.HasCheckConstraint("ck_refresh_tokens_expiry", "expires_at > created_at");
        });

        // 哈希即主键语义：令牌全局唯一
        builder.HasKey(x => x.TokenHash);

        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.UserId })
            .HasDatabaseName("ix_refresh_tokens_user");

        // 同模块复合外键：令牌必须归属本租户用户（模型声明，迁移自动生成）
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.UserId })
            .HasPrincipalKey(u => new { u.TenantId, u.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.ExpiresAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.RevokedAt).HasColumnType("timestamptz");
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
    }
}
