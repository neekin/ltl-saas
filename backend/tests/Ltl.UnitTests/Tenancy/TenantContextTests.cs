using Ltl.SharedKernel.Application;
using Xunit;

namespace Ltl.UnitTests.Tenancy;

public class TenantContextTests
{
    [Fact]
    public void InitialContext_ShouldBeEmpty()
    {
        var context = new TenantContext();

        Assert.Null(context.TenantId);
        Assert.Throws<TenantContextMissingException>(() => context.RequireTenant());
    }

    [Fact]
    public void SetTenant_ShouldExposeTenantId()
    {
        var context = new TenantContext();
        var tenantId = Guid.NewGuid();

        context.SetTenant(tenantId);

        Assert.Equal(tenantId, context.TenantId);
        Assert.Equal(tenantId, context.RequireTenant());
    }

    [Fact]
    public void SetTenant_WithEmptyGuid_ShouldThrow()
    {
        var context = new TenantContext();

        Assert.Throws<ArgumentException>(() => context.SetTenant(Guid.Empty));
    }

    [Fact]
    public void ClearTenant_ShouldRemoveContext()
    {
        var context = new TenantContext();
        context.SetTenant(Guid.NewGuid());

        context.ClearTenant();

        Assert.Null(context.TenantId);
        Assert.Throws<TenantContextMissingException>(() => context.RequireTenant());
    }
}
