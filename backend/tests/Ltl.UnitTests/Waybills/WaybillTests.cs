using Ltl.Modules.Waybills.Domain;
using Xunit;

namespace Ltl.UnitTests.Waybills;

/// <summary>
/// WB-001 运单聚合领域测试：创建守卫、六类状态初始分离、运单状态机、Draft 调整边界、汇总口径。
/// </summary>
public class WaybillTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OriginBranchId = Guid.NewGuid();
    private static readonly Guid DestBranchId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private static WaybillPackageDraft Pkg(
        string name = "纸箱", int quantity = 2, decimal weight = 12.345m, decimal volume = 0.1234m) => new(
        name, quantity, weight, volume);

    private static Waybill CreateWaybill(
        IReadOnlyList<WaybillPackageDraft>? packages = null,
        decimal? codAmount = null,
        string waybillNo = "YBGZ0120261117000001") =>
        Waybill.Create(
            TenantId, waybillNo, OriginBranchId, DestBranchId, CustomerId,
            WaybillPaymentType.Cash, "张三", "13800000000", "广州市白云区XX街1号",
            "李四", "13900000000", "武汉市汉阳区YY路2号",
            packages ?? [Pkg()], Now, codAmount: codAmount);

    // ---------- 创建守卫 ----------

    [Fact]
    public void Create_ShouldInitializeSixSeparatedStatuses()
    {
        var waybill = CreateWaybill();

        Assert.Equal(WaybillStatus.Draft, waybill.Status);
        Assert.Equal(WaybillCargoStatus.PendingReceive, waybill.CargoStatus);
        Assert.Equal(WaybillTransportStatus.NotShipped, waybill.TransportStatus);
        Assert.Equal(WaybillDeliveryStatus.NotDelivered, waybill.DeliveryStatus);
        Assert.Equal(WaybillFreightStatus.Unpaid, waybill.FreightStatus);
        Assert.Equal(WaybillCodStatus.None, waybill.CodStatus);
        Assert.Null(waybill.TotalAmount);
    }

    [Fact]
    public void Create_WithCodAmount_ShouldStartPendingCollection()
    {
        var waybill = CreateWaybill(codAmount: 500m);
        Assert.Equal(WaybillCodStatus.PendingCollection, waybill.CodStatus);
        Assert.Equal(500m, waybill.CodAmount);
    }

    [Fact]
    public void Create_WithZeroCod_ShouldBeNone()
    {
        var waybill = CreateWaybill(codAmount: 0m);
        Assert.Equal(WaybillCodStatus.None, waybill.CodStatus);
        Assert.Null(waybill.CodAmount);
    }

    [Fact]
    public void Create_WithNegativeCod_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => CreateWaybill(codAmount: -1m));
    }

    [Fact]
    public void Create_WithSameOriginAndDest_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            () => Waybill.Create(
                TenantId, "WB1", OriginBranchId, OriginBranchId, CustomerId,
                WaybillPaymentType.Cash, "张三", "1", "A", "李四", "2", "B",
                [Pkg()], Now));
    }

    [Fact]
    public void Create_WithEmptyPackages_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => CreateWaybill(packages: []));
    }

    [Fact]
    public void Create_WithBlankWaybillNo_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => CreateWaybill(waybillNo: "  "));
    }

    [Fact]
    public void Create_ShouldNumberPackagesFromOne()
    {
        var waybill = CreateWaybill(packages: [Pkg(), Pkg("木架", 1, 30m, 0.5m), Pkg("散货", 3, 5m, 0.02m)]);

        Assert.Equal([1, 2, 3], waybill.Packages.Select(p => p.PackageNo));
    }

    [Fact]
    public void Create_WithInvalidPackage_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => CreateWaybill(packages: [Pkg(quantity: 0)]));
        Assert.Throws<ArgumentException>(() => CreateWaybill(packages: [Pkg(weight: -1m)]));
        Assert.Throws<ArgumentException>(() => CreateWaybill(packages: [Pkg(volume: -0.1m)]));
        Assert.Throws<ArgumentException>(() => CreateWaybill(packages: [Pkg(name: " ")]));
    }

    [Fact]
    public void Create_WithBlankSenderOrReceiver_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => Waybill.Create(
            TenantId, "WB1", OriginBranchId, DestBranchId, CustomerId,
            WaybillPaymentType.Cash, " ", "1", "A", "李四", "2", "B", [Pkg()], Now));
        Assert.Throws<ArgumentException>(() => Waybill.Create(
            TenantId, "WB1", OriginBranchId, DestBranchId, CustomerId,
            WaybillPaymentType.Cash, "张三", "1", "A", "李四", "2", " ", [Pkg()], Now));
    }

    // ---------- 汇总口径 ----------

    [Fact]
    public void Totals_ShouldSumAcrossPackages()
    {
        var waybill = CreateWaybill(packages:
        [
            Pkg("A", quantity: 2, weight: 12.345m, volume: 0.1234m),
            Pkg("B", quantity: 3, weight: 5m, volume: 0.02m),
        ]);

        Assert.Equal(5, waybill.TotalPieces);
        Assert.Equal(17.345m, waybill.TotalWeightKg);
        Assert.Equal(0.1434m, waybill.TotalVolumeM3);
    }

    // ---------- 运单状态机 ----------

    [Fact]
    public void Confirm_FromDraft_ShouldSucceed()
    {
        var waybill = CreateWaybill();
        waybill.Confirm(DateTime.UtcNow, Guid.NewGuid());

        Assert.Equal(WaybillStatus.Confirmed, waybill.Status);
    }

    [Fact]
    public void Confirm_AfterConfirmedOrCancelled_ShouldThrow()
    {
        var waybill = CreateWaybill();
        waybill.Confirm(DateTime.UtcNow, null);
        Assert.Throws<WaybillDomainException>(() => waybill.Confirm(DateTime.UtcNow, null));

        waybill.Cancel(DateTime.UtcNow, null);
        Assert.Throws<WaybillDomainException>(() => waybill.Confirm(DateTime.UtcNow, null));
    }

    [Fact]
    public void Cancel_FromDraftAndConfirmed_ShouldSucceed()
    {
        var draft = CreateWaybill();
        draft.Cancel(DateTime.UtcNow, null);
        Assert.Equal(WaybillStatus.Cancelled, draft.Status);

        var confirmed = CreateWaybill();
        confirmed.Confirm(DateTime.UtcNow, null);
        confirmed.Cancel(DateTime.UtcNow, null);
        Assert.Equal(WaybillStatus.Cancelled, confirmed.Status);
    }

    [Fact]
    public void Cancel_AfterShipped_ShouldThrow()
    {
        var waybill = CreateWaybill();
        waybill.Confirm(DateTime.UtcNow, null);

        // 模拟已发运：运输状态由 P5 任务流转，此处通过领域字段验证取消守卫
        SetTransportStatus(waybill);
        Assert.Throws<WaybillDomainException>(() => waybill.Cancel(DateTime.UtcNow, null));
    }

    [Fact]
    public void Cancelled_ShouldBeTerminal()
    {
        var waybill = CreateWaybill();
        waybill.Cancel(DateTime.UtcNow, null);

        Assert.Throws<WaybillDomainException>(() => waybill.Cancel(DateTime.UtcNow, null));
        Assert.Throws<WaybillDomainException>(() => waybill.Confirm(DateTime.UtcNow, null));
        Assert.Throws<WaybillDomainException>(() => waybill.AddPackage(Pkg(), DateTime.UtcNow, null));
    }

    // ---------- Draft 调整边界 ----------

    [Fact]
    public void PackageAdjustments_AfterConfirm_ShouldBeRejected()
    {
        var waybill = CreateWaybill();
        waybill.Confirm(DateTime.UtcNow, null);

        Assert.Throws<WaybillDomainException>(() => waybill.AddPackage(Pkg(), DateTime.UtcNow, null));
        Assert.Throws<WaybillDomainException>(
            () => waybill.RemovePackage(waybill.Packages[0].Id, DateTime.UtcNow, null));
    }

    [Fact]
    public void RemovePackage_ShouldRenumberContinuously_AndKeepAtLeastOne()
    {
        var waybill = CreateWaybill(packages: [Pkg("A"), Pkg("B"), Pkg("C")]);
        var secondId = waybill.Packages[1].Id;

        waybill.RemovePackage(secondId, DateTime.UtcNow, null);

        Assert.Equal(2, waybill.Packages.Count);
        Assert.Equal([1, 2], waybill.Packages.Select(p => p.PackageNo));

        waybill.RemovePackage(waybill.Packages[0].Id, DateTime.UtcNow, null);
        Assert.Throws<WaybillDomainException>(
            () => waybill.RemovePackage(waybill.Packages[0].Id, DateTime.UtcNow, null));
    }

    // ---------- 费用固化 ----------

    [Fact]
    public void ApplyPricing_ShouldSetTotalAmountOnce()
    {
        var waybill = CreateWaybill();
        waybill.ApplyPricing(88.5m, DateTime.UtcNow, null);

        Assert.Equal(88.5m, waybill.TotalAmount);
        Assert.Throws<WaybillDomainException>(() => waybill.ApplyPricing(99m, DateTime.UtcNow, null));
        Assert.Throws<ArgumentException>(() => CreateWaybillForNegativePricing());
    }

    [Fact]
    public void ApplyPricing_AfterConfirm_ShouldBeRejected()
    {
        var waybill = CreateWaybill();
        waybill.Confirm(DateTime.UtcNow, null);

        Assert.Throws<WaybillDomainException>(() => waybill.ApplyPricing(88.5m, DateTime.UtcNow, null));
    }

    private static Waybill CreateWaybillForNegativePricing()
    {
        var waybill = CreateWaybill();
        waybill.ApplyPricing(-1m, DateTime.UtcNow, null);
        return waybill;
    }

    // ---------- P10 客户查询码 ----------

    [Fact]
    public void SetQueryCode_ShouldStoreHashOnly_AndVerify()
    {
        var waybill = CreateWaybill();
        var code = Waybill.GenerateQueryCode();

        waybill.SetQueryCode(code, Now);

        Assert.NotNull(waybill.QueryCodeHash);
        Assert.DoesNotContain(code, waybill.QueryCodeHash);
        Assert.True(waybill.VerifyQueryCode(code));
    }

    [Fact]
    public void VerifyQueryCode_ShouldBeCaseInsensitive_AndRejectWrongOrNull()
    {
        var waybill = CreateWaybill();
        var code = Waybill.GenerateQueryCode();
        waybill.SetQueryCode(code, Now);

        Assert.True(waybill.VerifyQueryCode(code.ToLowerInvariant()));
        Assert.False(waybill.VerifyQueryCode("WRONGCODE"));
        Assert.False(waybill.VerifyQueryCode(string.Empty));

        var withoutCode = CreateWaybill(waybillNo: "YBGZ0120261117000002");
        Assert.False(withoutCode.VerifyQueryCode(code));
    }

    [Fact]
    public void ResetQueryCode_ShouldInvalidateOldCode()
    {
        var waybill = CreateWaybill();
        var oldCode = Waybill.GenerateQueryCode();
        var newCode = Waybill.GenerateQueryCode();
        waybill.SetQueryCode(oldCode, Now);

        waybill.SetQueryCode(newCode, Now);

        Assert.False(waybill.VerifyQueryCode(oldCode));
        Assert.True(waybill.VerifyQueryCode(newCode));
    }

    [Fact]
    public void GenerateQueryCode_ShouldBeUnambiguousEightChars()
    {
        var code = Waybill.GenerateQueryCode();
        Assert.Equal(8, code.Length);
        Assert.DoesNotContain('0', code);
        Assert.DoesNotContain('O', code);
        Assert.DoesNotContain('1', code);
        Assert.DoesNotContain('I', code);
    }

    /// <summary>测试辅助：模拟 P5 任务将来的运输状态流转（WB-001 未交付该流转，仅验证取消守卫）。</summary>
    private static void SetTransportStatus(Waybill waybill)
    {
        var property = typeof(Waybill).GetProperty(nameof(Waybill.TransportStatus))
            ?? throw new InvalidOperationException("TransportStatus 属性缺失");
        property.SetValue(waybill, (WaybillTransportStatus)99);
    }
}
