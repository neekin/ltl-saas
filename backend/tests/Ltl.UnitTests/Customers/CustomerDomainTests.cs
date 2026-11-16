using Ltl.Modules.Customers.Domain;
using Xunit;

namespace Ltl.UnitTests.Customers;

public class CustomerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldNormalizeAndSetActive()
    {
        var customer = Customer.Create(TenantId, " c001 ", "深圳某电子厂  ", DateTime.UtcNow, remark: " 月结大客户 ");

        Assert.Equal("c001", customer.Code);
        Assert.Equal("深圳某电子厂", customer.Name);
        Assert.Equal("月结大客户", customer.Remark);
        Assert.Equal(CustomerStatus.Active, customer.Status);
        Assert.Equal(TenantId, customer.TenantId);
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            () => Customer.Create(Guid.Empty, "c001", "深圳某电子厂", DateTime.UtcNow));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankCode_ShouldThrow(string? code)
    {
        Assert.Throws<ArgumentException>(() => Customer.Create(TenantId, code!, "深圳某电子厂", DateTime.UtcNow));
    }

    [Fact]
    public void Create_WithOverLengthName_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            () => Customer.Create(TenantId, "c001", new string('厂', 201), DateTime.UtcNow));
    }

    [Fact]
    public void UpdateProfile_ShouldReplaceFieldsAndTouch()
    {
        var customer = Customer.Create(TenantId, "c001", "旧名称", DateTime.UtcNow);
        var now = DateTime.UtcNow;

        customer.UpdateProfile("新名称", "新备注", now);

        Assert.Equal("新名称", customer.Name);
        Assert.Equal("新备注", customer.Remark);
        Assert.Equal(now, customer.UpdatedAt);
    }

    [Fact]
    public void Disable_ShouldBeIdempotent()
    {
        var customer = Customer.Create(TenantId, "c001", "深圳某电子厂", DateTime.UtcNow);
        var now = DateTime.UtcNow;

        customer.Disable(now);
        customer.Disable(now.AddMinutes(1));

        Assert.Equal(CustomerStatus.Disabled, customer.Status);
        Assert.Equal(now, customer.UpdatedAt);
    }
}

public class CustomerContactTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldSetFields()
    {
        var contact = CustomerContact.Create(
            TenantId, CustomerId, "王经理", "13800138000", DateTime.UtcNow, isPrimary: true);

        Assert.Equal("王经理", contact.Name);
        Assert.Equal("13800138000", contact.Phone);
        Assert.True(contact.IsPrimary);
        Assert.Equal(CustomerId, contact.CustomerId);
    }

    [Fact]
    public void Create_WithEmptyCustomerId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            () => CustomerContact.Create(TenantId, Guid.Empty, "王经理", "13800138000", DateTime.UtcNow));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Create_WithBlankPhone_ShouldThrow(string? phone)
    {
        Assert.Throws<ArgumentException>(
            () => CustomerContact.Create(TenantId, CustomerId, "王经理", phone!, DateTime.UtcNow));
    }
}

public class CustomerAddressTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldSetFields()
    {
        var address = CustomerAddress.Create(
            TenantId, CustomerId, "深圳市南山区科技园 1 号", DateTime.UtcNow, label: "仓库", isDefault: true);

        Assert.Equal("深圳市南山区科技园 1 号", address.Address);
        Assert.Equal("仓库", address.Label);
        Assert.True(address.IsDefault);
    }

    [Fact]
    public void Create_WithBlankAddress_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            () => CustomerAddress.Create(TenantId, CustomerId, "   ", DateTime.UtcNow));
    }
}
