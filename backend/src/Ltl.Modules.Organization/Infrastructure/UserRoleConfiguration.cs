using Ltl.Modules.Organization.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Organization.Infrastructure;

/// <summary>user_roles 关联表映射：复合主键 (tenant_id, user_id, role_id)，无独立 id 列。</summary>
public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");

        builder.HasKey(x => new { x.TenantId, x.UserId, x.RoleId });
    }
}
