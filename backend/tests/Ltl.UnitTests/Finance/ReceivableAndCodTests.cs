using Ltl.Modules.Finance.Domain;
using Xunit;

namespace Ltl.UnitTests.Finance;

/// <summary>财务与 COD 领域规则单元测试（AGENTS §6.5、§10）。</summary>
public sealed class ReceivableAndCodTests
{
    private static readonly DateTime NowUtc = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid WaybillId = Guid.NewGuid();

    [Fact]
    public void Receivable_PartialFullAndReverseSettlement_ShouldTrackBalancesAndStatuses()
    {
        var receivable = Receivable.Create(TenantId, WaybillId, 1000m, NowUtc);
        Assert.Equal(ReceivableStatus.Unpaid, receivable.Status);
        Assert.Equal(1000m, receivable.Outstanding);

        // 部分收款
        receivable.ApplySettlement(400m);
        Assert.Equal(ReceivableStatus.PartiallyPaid, receivable.Status);
        Assert.Equal(400m, receivable.SettledAmount);
        Assert.Equal(600m, receivable.Outstanding);

        // 超额核销 → 拒绝
        Assert.Throws<InvalidOperationException>(() => receivable.ApplySettlement(600.01m));

        // 尾款结清
        receivable.ApplySettlement(600m);
        Assert.Equal(ReceivableStatus.Settled, receivable.Status);
        Assert.Equal(0m, receivable.Outstanding);

        // 部分退款回退为 PartiallyPaid
        receivable.ReverseSettlement(250m);
        Assert.Equal(ReceivableStatus.PartiallyPaid, receivable.Status);
        Assert.Equal(750m, receivable.SettledAmount);

        // 退清回退为 Unpaid
        receivable.ReverseSettlement(750m);
        Assert.Equal(ReceivableStatus.Unpaid, receivable.Status);
        Assert.Equal(0m, receivable.SettledAmount);

        // 超额退款 → 拒绝
        Assert.Throws<InvalidOperationException>(() => receivable.ReverseSettlement(1m));
    }

    [Fact]
    public void Receivable_Create_WithInvalidAmountOrNonUtc_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => Receivable.Create(TenantId, WaybillId, 0m, NowUtc));
        Assert.Throws<ArgumentException>(() => Receivable.Create(TenantId, WaybillId, -10m, NowUtc));
        Assert.Throws<ArgumentException>(() =>
            Receivable.Create(TenantId, WaybillId, 100m, new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Local)));
    }

    [Fact]
    public void CodOrder_FullLifecycleAndBoundaries_ShouldEnforceInvariants()
    {
        var cod = CodOrder.Create(TenantId, WaybillId, 500m, NowUtc);
        Assert.Equal(CodOrderStatus.PendingCollection, cod.Status);
        Assert.Equal(500m, cod.OutstandingCollection);

        // 收款 300
        cod.ApplyCollection(300m);
        Assert.Equal(CodOrderStatus.Collected, cod.Status);
        Assert.Equal(200m, cod.OutstandingCollection);
        Assert.Equal(300m, cod.AvailableForSettlement);

        // 超额收款 → 拒绝
        Assert.Throws<InvalidOperationException>(() => cod.ApplyCollection(200.01m));

        // 超额结算（超过已收 300）→ 拒绝
        Assert.Throws<InvalidOperationException>(() => cod.ApplySettlement(300.01m));

        // 结算 200
        cod.ApplySettlement(200m);
        Assert.Equal(CodOrderStatus.Settled, cod.Status);
        Assert.Equal(100m, cod.AvailableForSettlement);
        Assert.Equal(200m, cod.AvailableForPayment);

        // 超额付款 → 拒绝
        Assert.Throws<InvalidOperationException>(() => cod.ApplyPayment(200.01m));

        // 付款 200 → PaidOut
        cod.ApplyPayment(200m);
        Assert.Equal(CodOrderStatus.PaidOut, cod.Status);
        Assert.Equal(0m, cod.AvailableForPayment);
    }

    [Fact]
    public void CodOrder_ReverseCollection_ToZero_ShouldResetToPendingCollection()
    {
        var cod = CodOrder.Create(TenantId, WaybillId, 500m, NowUtc);
        cod.ApplyCollection(200m);
        Assert.Equal(CodOrderStatus.Collected, cod.Status);

        cod.ReverseCollection(200m);
        Assert.Equal(CodOrderStatus.PendingCollection, cod.Status);
        Assert.Equal(0m, cod.CollectedAmount);

        Assert.Throws<InvalidOperationException>(() => cod.ReverseCollection(1m));
    }

    [Fact]
    public void CodSettlement_MustApproveBeforePay_AndRejectDoubleApprove()
    {
        var settlement = CodSettlement.Create(TenantId, Guid.NewGuid(), 200m, Guid.NewGuid(), NowUtc);
        Assert.Equal(CodSettlementStatus.Draft, settlement.Status);

        // 未审批不可付款（AGENTS §6.5）
        Assert.Throws<InvalidOperationException>(() => settlement.MarkPaidOut(Guid.NewGuid(), NowUtc));

        var approverId = Guid.NewGuid();
        settlement.Approve(approverId, NowUtc);
        Assert.Equal(CodSettlementStatus.Approved, settlement.Status);
        Assert.Equal(approverId, settlement.ApprovedBy);

        // 不可重复审批
        Assert.Throws<InvalidOperationException>(() => settlement.Approve(approverId, NowUtc));

        var payerId = Guid.NewGuid();
        settlement.MarkPaidOut(payerId, NowUtc);
        Assert.Equal(CodSettlementStatus.PaidOut, settlement.Status);
        Assert.Equal(payerId, settlement.PaidBy);

        // 不可重复付款
        Assert.Throws<InvalidOperationException>(() => settlement.MarkPaidOut(payerId, NowUtc));
    }

    [Fact]
    public void CustomerStatement_LifecycleGuards_ShouldPreventInvalidTransitions()
    {
        Assert.Throws<ArgumentException>(() =>
            CustomerStatement.Create(TenantId, Guid.NewGuid(), "2026-11", 100m, NowUtc));

        var statement = CustomerStatement.Create(TenantId, Guid.NewGuid(), "202611", 1200m, NowUtc);
        Assert.Equal(CustomerStatementStatus.Draft, statement.Status);

        // 草稿不可直接关闭
        Assert.Throws<InvalidOperationException>(() => statement.Close(NowUtc, Guid.NewGuid()));

        statement.Confirm(NowUtc, Guid.NewGuid());
        Assert.Equal(CustomerStatementStatus.Confirmed, statement.Status);

        // 已确认不可重复确认
        Assert.Throws<InvalidOperationException>(() => statement.Confirm(NowUtc, Guid.NewGuid()));

        statement.Close(NowUtc, Guid.NewGuid());
        Assert.Equal(CustomerStatementStatus.Closed, statement.Status);

        // 已关闭终态不可再确认或关闭
        Assert.Throws<InvalidOperationException>(() => statement.Confirm(NowUtc, Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => statement.Close(NowUtc, Guid.NewGuid()));
    }
}
