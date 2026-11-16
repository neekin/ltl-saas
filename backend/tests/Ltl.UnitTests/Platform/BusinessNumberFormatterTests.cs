using Ltl.Modules.Platform.Domain;
using Xunit;

namespace Ltl.UnitTests.Platform;

public class BusinessNumberFormatterTests
{
    [Fact]
    public void Format_Waybill_ShouldFollowSuggestedFormat()
    {
        var number = BusinessNumberFormatter.Format(
            BusinessDocumentType.Waybill, "GZ01", new DateOnly(2026, 11, 17), 1);

        Assert.Equal("YBGZ0120261117000001", number);
    }

    [Theory]
    [InlineData(BusinessDocumentType.Waybill, "YB")]
    [InlineData(BusinessDocumentType.BranchTransfer, "DB")]
    [InlineData(BusinessDocumentType.TransportTrip, "PC")]
    public void Prefix_ShouldFollowSuggestedMapping(BusinessDocumentType docType, string expected)
    {
        Assert.Equal(expected, BusinessNumberFormatter.Prefix(docType));
    }

    [Fact]
    public void Format_ShouldPadSequenceToSixDigits()
    {
        var number = BusinessNumberFormatter.Format(
            BusinessDocumentType.BranchTransfer, "WH02", new DateOnly(2026, 1, 3), 42);

        Assert.Equal("DBWH0220260103000042", number);
    }

    [Fact]
    public void Format_AtCapacityLimit_ShouldSucceed()
    {
        var number = BusinessNumberFormatter.Format(
            BusinessDocumentType.TransportTrip, "CS", new DateOnly(2026, 11, 17), 999_999);

        Assert.EndsWith("999999", number);
    }

    [Fact]
    public void Format_OverCapacity_ShouldThrowExplicitError()
    {
        Assert.Throws<InvalidOperationException>(
            () => BusinessNumberFormatter.Format(
                BusinessDocumentType.Waybill, "GZ01", new DateOnly(2026, 11, 17), 1_000_000));
    }

    [Fact]
    public void Format_WithBlankBranchCode_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            () => BusinessNumberFormatter.Format(BusinessDocumentType.Waybill, " ", new DateOnly(2026, 11, 17), 1));
    }

    [Fact]
    public void Period_ShouldUseyyyyMMdd()
    {
        Assert.Equal("20261117", BusinessNumberFormatter.Period(new DateOnly(2026, 11, 17)));
    }
}
