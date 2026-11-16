using Ltl.Modules.Organization.Domain;
using Xunit;

namespace Ltl.UnitTests.Organization;

public class BranchTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldNormalizeAndSetActive()
    {
        var branch = Branch.Create(TenantId, " gz01 ", "广州营业部  ", DateTime.UtcNow);

        Assert.Equal("gz01", branch.Code);
        Assert.Equal("广州营业部", branch.Name);
        Assert.Equal(BranchStatus.Active, branch.Status);
        Assert.Equal(TenantId, branch.TenantId);
        Assert.NotEqual(Guid.Empty, branch.Id);
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            () => Branch.Create(Guid.Empty, "gz01", "广州营业部", DateTime.UtcNow));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankCode_ShouldThrow(string? code)
    {
        Assert.Throws<ArgumentException>(() => Branch.Create(TenantId, code!, "广州营业部", DateTime.UtcNow));
    }

    [Fact]
    public void Create_WithOverLengthName_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            () => Branch.Create(TenantId, "gz01", new string('长', 201), DateTime.UtcNow));
    }

    [Fact]
    public void Disable_ShouldBeIdempotent()
    {
        var branch = Branch.Create(TenantId, "gz01", "广州营业部", DateTime.UtcNow);
        var now = DateTime.UtcNow;

        branch.Disable(now);
        branch.Disable(now.AddMinutes(1));

        Assert.Equal(BranchStatus.Disabled, branch.Status);
        Assert.Equal(now, branch.UpdatedAt);
    }

    [Fact]
    public void Enable_ShouldRestoreActive()
    {
        var branch = Branch.Create(TenantId, "gz01", "广州营业部", DateTime.UtcNow);
        branch.Disable(DateTime.UtcNow);

        branch.Enable(DateTime.UtcNow);

        Assert.Equal(BranchStatus.Active, branch.Status);
    }
}
