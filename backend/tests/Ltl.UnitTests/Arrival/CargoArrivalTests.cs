using Ltl.Modules.Arrivals.Domain;
using Xunit;

namespace Ltl.UnitTests.Arrival;

/// <summary>到货登记与到货差异明细领域校验单元测试。</summary>
public sealed class CargoArrivalTests
{
    private static readonly DateTime NowUtc = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid TripId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidItemsAndDamage_ShouldPreserveDifferenceRecords()
    {
        var waybillId = Guid.NewGuid();
        var arrival = CargoArrival.Create(
            TenantId, TripId, BranchId,
            [(waybillId, LoadedPieces: 10, ReceivedPieces: 8, BrokenPieces: 2, Remark: "少件2、外箱破损2")],
            operatorUserId: Guid.NewGuid(),
            note: "  夜间到车  ",
            arrivedAtUtc: NowUtc);

        Assert.Single(arrival.Items);
        Assert.Equal("夜间到车", arrival.Note);
        Assert.Equal(10, arrival.Items[0].LoadedPieces);
        Assert.Equal(8, arrival.Items[0].ReceivedPieces);
        Assert.Equal(2, arrival.Items[0].BrokenPieces);
    }

    [Fact]
    public void Create_WithEmptyItemsOrNonUtc_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            CargoArrival.Create(TenantId, TripId, BranchId, [], null, null, NowUtc));

        Assert.Throws<ArgumentException>(() =>
            CargoArrival.Create(
                TenantId, TripId, BranchId,
                [(Guid.NewGuid(), 5, 5, 0, null)],
                null, null, new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Local)));
    }

    [Theory]
    [InlineData(0, 0, 0)]   // 装车件数 < 1
    [InlineData(5, -1, 0)]  // 实收负数
    [InlineData(5, 5, -1)]  // 破损负数
    [InlineData(5, 3, 4)]   // 破损 > 实收
    [InlineData(5, 6, 0)]   // 实收 > 装车
    public void ArrivalItem_InvalidCounts_ShouldThrow(int loaded, int received, int broken)
    {
        Assert.Throws<ArgumentException>(() =>
            ArrivalItem.Create(TenantId, Guid.NewGuid(), Guid.NewGuid(), loaded, received, broken, null, NowUtc));
    }
}
