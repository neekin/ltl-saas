using Ltl.Modules.Warehouse.Domain;
using Xunit;

namespace Ltl.UnitTests.Warehouse;

/// <summary>网点调拨单状态机与验收差异领域单元测试（AGENTS §6.3）。</summary>
public sealed class BranchTransferTests
{
    private static readonly DateTime NowUtc = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FromBranchId = Guid.NewGuid();
    private static readonly Guid ToBranchId = Guid.NewGuid();

    [Fact]
    public void Create_WithSameBranchOrEmptyItems_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => BranchTransfer.Create(
            TenantId, "DBGZ0120261119000001", FromBranchId, FromBranchId,
            [("托盘", 5, null)], NowUtc));

        Assert.Throws<ArgumentException>(() => BranchTransfer.Create(
            TenantId, "DBGZ0120261119000001", FromBranchId, ToBranchId,
            [], NowUtc));
    }

    [Fact]
    public void Lifecycle_DraftToSubmittedToDispatchedToReceived_ShouldEnforceGuards()
    {
        var operatorId = Guid.NewGuid();
        var transfer = BranchTransfer.Create(
            TenantId, "DBGZ0120261119000001", FromBranchId, ToBranchId,
            [("电器箱", 10, null)], NowUtc, operatorId);

        Assert.Equal(BranchTransferStatus.Draft, transfer.Status);
        var item = Assert.Single(transfer.Items);

        // 草稿不可直接发出或验收
        Assert.Throws<WarehouseDomainException>(() => transfer.Dispatch(NowUtc, operatorId));
        Assert.Throws<WarehouseDomainException>(() =>
            transfer.Receive([(item.Id, 10, 0, null)], NowUtc, operatorId));

        transfer.Submit(NowUtc, operatorId);
        Assert.Equal(BranchTransferStatus.Submitted, transfer.Status);

        transfer.Dispatch(NowUtc, operatorId);
        Assert.Equal(BranchTransferStatus.Dispatched, transfer.Status);

        // 已发出不可取消
        Assert.Throws<WarehouseDomainException>(() => transfer.Cancel(NowUtc, operatorId));

        // 缺少明细项验收 → 拒绝
        Assert.Throws<WarehouseDomainException>(() => transfer.Receive([], NowUtc, operatorId));

        // 验收含短少与破损差异留痕
        transfer.Receive([(item.Id, 9, 1, "短少1、破损1")], NowUtc, operatorId);
        Assert.Equal(BranchTransferStatus.Received, transfer.Status);
        Assert.Equal(9, item.ReceivedQuantity);
        Assert.Equal(1, item.BrokenQuantity);

        // 已验收终态不可重复验收
        Assert.Throws<WarehouseDomainException>(() =>
            transfer.Receive([(item.Id, 9, 1, null)], NowUtc, operatorId));
    }

    [Fact]
    public void Cancel_FromDraftOrSubmitted_ShouldBeTerminal()
    {
        var operatorId = Guid.NewGuid();
        var transfer = BranchTransfer.Create(
            TenantId, "DBGZ0120261119000002", FromBranchId, ToBranchId,
            [("配件", 3, null)], NowUtc, operatorId);

        transfer.Cancel(NowUtc, operatorId);
        Assert.Equal(BranchTransferStatus.Cancelled, transfer.Status);
        Assert.Throws<WarehouseDomainException>(() => transfer.Submit(NowUtc, operatorId));
        Assert.Throws<WarehouseDomainException>(() => transfer.Cancel(NowUtc, operatorId));
    }
}
