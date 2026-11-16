using Ltl.Modules.Tenancy.Domain;
using Xunit;

namespace Ltl.UnitTests.Tenancy;

public class TenantTests
{
    [Fact]
    public void Create_ShouldNormalizeAndSetActive()
    {
        var tenant = Tenant.Create(" acme ", "某专线物流  ", DateTime.UtcNow);

        Assert.Equal("acme", tenant.Code);
        Assert.Equal("某专线物流", tenant.Name);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.NotEqual(Guid.Empty, tenant.Id);
        Assert.Null(tenant.UpdatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankCode_ShouldThrow(string? code)
    {
        Assert.Throws<ArgumentException>(() => Tenant.Create(code!, "某专线物流", DateTime.UtcNow));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_ShouldThrow(string? name)
    {
        Assert.Throws<ArgumentException>(() => Tenant.Create("acme", name!, DateTime.UtcNow));
    }

    [Fact]
    public void Create_WithUnspecifiedKind_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            () => Tenant.Create("acme", "某专线物流", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void Disable_ShouldSetStatusAndTouchOnce()
    {
        var tenant = Tenant.Create("acme", "某专线物流", DateTime.UtcNow);
        var now = DateTime.UtcNow;

        tenant.Disable(now);
        tenant.Disable(now.AddMinutes(1)); // 幂等

        Assert.Equal(TenantStatus.Disabled, tenant.Status);
        Assert.Equal(now, tenant.UpdatedAt);
    }

    [Fact]
    public void Enable_ShouldRestoreActiveStatus()
    {
        var tenant = Tenant.Create("acme", "某专线物流", DateTime.UtcNow);
        tenant.Disable(DateTime.UtcNow);

        tenant.Enable(DateTime.UtcNow);
        tenant.Enable(DateTime.UtcNow.AddMinutes(1)); // 幂等

        Assert.Equal(TenantStatus.Active, tenant.Status);
    }
}
