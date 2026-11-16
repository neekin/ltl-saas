using Ltl.Modules.Organization.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ltl.Modules.Organization.Infrastructure;

/// <summary>user_branch_scopes 关联表映射：复合主键 (tenant_id, user_id, branch_id)。</summary>
public sealed class UserBranchScopeConfiguration : IEntityTypeConfiguration<UserBranchScope>
{
    public void Configure(EntityTypeBuilder<UserBranchScope> builder)
    {
        builder.ToTable("user_branch_scopes");

        builder.HasKey(x => new { x.TenantId, x.UserId, x.BranchId });
    }
}
