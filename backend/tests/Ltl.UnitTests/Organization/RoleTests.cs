using Ltl.Modules.Organization.Domain;
using Xunit;

namespace Ltl.UnitTests.Organization;

public class RoleTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldNormalizeAndSetActive()
    {
        var role = Role.Create(TenantId, " ops_admin ", "运营管理员 ", DateTime.UtcNow);

        Assert.Equal("ops_admin", role.Code);
        Assert.Equal("运营管理员", role.Name);
        Assert.Equal(RoleStatus.Active, role.Status);
        Assert.Equal(TenantId, role.TenantId);
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            () => Role.Create(Guid.Empty, "ops_admin", "运营管理员", DateTime.UtcNow));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_ShouldThrow(string? name)
    {
        Assert.Throws<ArgumentException>(() => Role.Create(TenantId, "ops_admin", name!, DateTime.UtcNow));
    }

    [Fact]
    public void Disable_ShouldBeIdempotent()
    {
        var role = Role.Create(TenantId, "ops_admin", "运营管理员", DateTime.UtcNow);
        var now = DateTime.UtcNow;

        role.Disable(now);
        role.Disable(now.AddMinutes(1));

        Assert.Equal(RoleStatus.Disabled, role.Status);
        Assert.Equal(now, role.UpdatedAt);
    }
}
