using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Organization.Domain;
using Xunit;

namespace Ltl.UnitTests.Organization;

public class PermissionEvaluatorTests
{
    [Fact]
    public void EffectivePermissions_ShouldUnionActiveRoles()
    {
        var permissions = PermissionEvaluator.EffectivePermissions(new[]
        {
            (RoleStatus.Active, (IReadOnlyCollection<string>)new[] { "organization.branch.read", "organization.branch.manage" }),
            (RoleStatus.Active, (IReadOnlyCollection<string>)new[] { "organization.branch.manage", "organization.user.read" }),
            (RoleStatus.Disabled, (IReadOnlyCollection<string>)new[] { "organization.role.manage" }),
        });

        Assert.Equal(
            new HashSet<string> { "organization.branch.read", "organization.branch.manage", "organization.user.read" },
            permissions.ToHashSet());
    }

    [Fact]
    public void EffectivePermissions_WithNoRoles_ShouldBeEmpty()
    {
        var permissions = PermissionEvaluator.EffectivePermissions(Array.Empty<(RoleStatus, IReadOnlyCollection<string>)>());

        Assert.Empty(permissions);
    }

    [Fact]
    public void EffectiveBranchIds_DisabledUser_ShouldBeEmpty()
    {
        var result = PermissionEvaluator.EffectiveBranchIds(
            UserStatus.Disabled,
            tenantWideScope: true,
            Array.Empty<(Guid, BranchStatus)>(),
            new[] { (Guid.NewGuid(), BranchStatus.Active) });

        Assert.Empty(result);
    }

    [Fact]
    public void EffectiveBranchIds_TenantWideScope_ShouldIncludeOnlyActiveBranches()
    {
        var activeId = Guid.NewGuid();
        var disabledId = Guid.NewGuid();

        var result = PermissionEvaluator.EffectiveBranchIds(
            UserStatus.Active,
            tenantWideScope: true,
            Array.Empty<(Guid, BranchStatus)>(),
            new[] { (activeId, BranchStatus.Active), (disabledId, BranchStatus.Disabled) });

        Assert.Equal(new HashSet<Guid> { activeId }, result.ToHashSet());
    }

    [Fact]
    public void EffectiveBranchIds_RowLevelScope_ShouldExcludeDisabledBranches()
    {
        var activeScoped = Guid.NewGuid();
        var disabledScoped = Guid.NewGuid();
        var activeTenant = Guid.NewGuid();

        var result = PermissionEvaluator.EffectiveBranchIds(
            UserStatus.Active,
            tenantWideScope: false,
            new[] { (activeScoped, BranchStatus.Active), (disabledScoped, BranchStatus.Disabled) },
            new[] { (activeTenant, BranchStatus.Active) });

        Assert.Equal(new HashSet<Guid> { activeScoped }, result.ToHashSet());
    }
}
