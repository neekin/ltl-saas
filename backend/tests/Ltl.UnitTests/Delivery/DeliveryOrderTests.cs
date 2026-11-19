using Ltl.Modules.Delivery.Domain;
using Xunit;

namespace Ltl.UnitTests.Delivery;

/// <summary>交付单领域状态机与守卫单元测试。</summary>
public sealed class DeliveryOrderTests
{
    private static readonly DateTime NowUtc = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid WaybillId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();

    [Fact]
    public void Create_WithInvalidTenantOrNonUtc_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            DeliveryOrder.Create(Guid.Empty, WaybillId, BranchId, DeliveryType.Delivery, null, null, NowUtc));
        Assert.Throws<ArgumentException>(() =>
            DeliveryOrder.Create(TenantId, WaybillId, BranchId, DeliveryType.Delivery, null, null,
                new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void Assign_AndComplete_ShouldTransitionToTerminalState()
    {
        var order = DeliveryOrder.Create(TenantId, WaybillId, BranchId, DeliveryType.Delivery, null, null, NowUtc);
        Assert.Equal(DeliveryOrderStatus.Pending, order.Status);

        var courierId = Guid.NewGuid();
        var dispatcherId = Guid.NewGuid();
        order.Assign(courierId, NowUtc, dispatcherId);
        Assert.Equal(courierId, order.AssigneeUserId);
        Assert.Equal(dispatcherId, order.AssignedBy);

        // 空白签收人拒绝
        Assert.Throws<ArgumentException>(() => order.Complete("   ", NowUtc, courierId));

        order.Complete("  李四  ", NowUtc, courierId);
        Assert.Equal(DeliveryOrderStatus.Completed, order.Status);
        Assert.Equal("李四", order.ReceiverName);

        // 终态不可再次签收、拒收或派单
        Assert.Throws<InvalidOperationException>(() => order.Complete("王五", NowUtc, courierId));
        Assert.Throws<InvalidOperationException>(() => order.Reject(NowUtc, courierId));
        Assert.Throws<InvalidOperationException>(() => order.Assign(Guid.NewGuid(), NowUtc, dispatcherId));
    }

    [Fact]
    public void Reject_ShouldTransitionToTerminalState()
    {
        var order = DeliveryOrder.Create(TenantId, WaybillId, BranchId, DeliveryType.SelfPickup, null, null, NowUtc);
        var operatorId = Guid.NewGuid();

        order.Reject(NowUtc, operatorId);
        Assert.Equal(DeliveryOrderStatus.Rejected, order.Status);
        Assert.Equal(operatorId, order.CompletedBy);

        Assert.Throws<InvalidOperationException>(() => order.Complete("张三", NowUtc, operatorId));
        Assert.Throws<InvalidOperationException>(() => order.Reject(NowUtc, operatorId));
    }
}
