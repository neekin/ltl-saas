using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ltl.Modules.Customers.Domain;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Platform.Application;
using Ltl.Modules.Pricing.Application;
using Ltl.Modules.Pricing.Domain;
using Ltl.Modules.Tenancy.Domain;
using Ltl.Modules.Warehouse.Domain;
using Ltl.Modules.Warehouse.Infrastructure;
using Ltl.Modules.Waybills.Application;
using Ltl.Modules.Waybills.Domain;
using Ltl.Modules.Waybills.Infrastructure;
using Ltl.SharedKernel.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ltl.IntegrationTests.Warehouse;

/// <summary>
/// RECEIVE-001 集成测试：收货登记（组合授权/状态守卫/事件/审计/事务回滚/分批收货/跨租户）。
/// </summary>
public class CargoReceiptTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public CargoReceiptTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    private HttpClient CreateClient()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("ConnectionStrings:Tenancy", _fixture.ConnectionString);
            });
        return factory.CreateClient();
    }

    /// <summary>seed：租户 + 双网点 + 客户 + 已发布价卡 + 已确认运单 + 收货员（权限 + 网点范围）。</summary>
    public async Task<(Guid TenantId, Guid BranchId, Guid WaybillId, Guid KeeperUserId, string TenantCode)>
        SeedConfirmedWaybillAsync(bool confirm = true)
    {
        var tenantCode = $"rcpt-{Guid.NewGuid():N}";
        var tenant = Tenant.Create(tenantCode, "收货测试租户", DateTime.UtcNow);
        await using (var tenancy = _fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenant);
            await tenancy.SaveChangesAsync();
        }

        await using var org = _fixture.CreateOrganizationContext(tenant.Id);
        var origin = Branch.Create(tenant.Id, "GZ01", "广州", DateTime.UtcNow);
        var dest = Branch.Create(tenant.Id, "WH02", "武汉", DateTime.UtcNow);
        org.Branches.AddRange(origin, dest);
        var keeper = User.Create(tenant.Id, "keeper", "收货员", PasswordHasher.Hash("secret-123"), DateTime.UtcNow);
        var keeperRole = Role.Create(tenant.Id, "keeper", "仓储员", DateTime.UtcNow);
        var admin = User.Create(tenant.Id, "admin", "管理员", PasswordHasher.Hash("secret-123"), DateTime.UtcNow);
        var adminRole = Role.Create(tenant.Id, "ops", "管理员", DateTime.UtcNow);
        org.Users.AddRange(keeper, admin);
        org.Roles.AddRange(keeperRole, adminRole);
        await org.SaveChangesAsync();

        await using var customers = _fixture.CreateCustomersContext(tenant.Id);
        var customer = Customer.Create(tenant.Id, "KH001", "测试客户", DateTime.UtcNow);
        customers.Customers.Add(customer);
        await customers.SaveChangesAsync();

        var authorization = new AuthorizationService(org);
        await authorization.GrantPermissionAsync(keeperRole.Id, WarehousePermissionCodes.CargoReceive);
        await authorization.GrantPermissionAsync(keeperRole.Id, WarehousePermissionCodes.CargoRead);
        await authorization.GrantPermissionAsync(keeperRole.Id, WarehousePermissionCodes.CargoSort);
        await authorization.AssignRoleAsync(keeper.Id, keeperRole.Id);
        await authorization.GrantBranchScopeAsync(keeper.Id, origin.Id);
        foreach (var permission in new[]
                 {
                     PermissionCodes.BranchRead, PermissionCodes.BranchManage,
                     PermissionCodes.UserRead, PermissionCodes.UserManage, PermissionCodes.RoleManage,
                 })
        {
            await authorization.GrantPermissionAsync(adminRole.Id, permission);
        }

        await authorization.AssignRoleAsync(admin.Id, adminRole.Id);

        // 发布价卡 + 创建并确认运单
        var pricing = new PricingRuleSetService(_fixture.CreatePricingContext(tenant.Id));
        var ruleSet = await pricing.CreateRuleSetAsync("std", "标准价卡", DateTime.UtcNow);
        var version = await pricing.CreateDraftVersionAsync(ruleSet.Id, new PricingRuleDefinition
        {
            Rounding = RoundingPolicy.HalfUpTwoDecimals,
            Rules =
            [
                new PricingRule
                {
                    Priority = 1,
                    Name = "标准运价",
                    Enabled = true,
                    BillingWeightStrategy = BillingWeightStrategy.WeightOnly,
                    Conditions = [],
                    Freight = new PricingFreightFeeItem
                    {
                        Name = "运费",
                        Type = PricingFreightType.ByWeight,
                        UnitPrice = 1.5m,
                    },
                    Surcharges = [],
                },
            ],
        }, DateTime.UtcNow);
        await pricing.PublishVersionAsync(version.Id, DateTime.UtcNow);

        var waybills = new WaybillService(
            _fixture.CreateWaybillsContext(tenant.Id),
            new BranchSequenceService(_fixture.CreatePlatformContext(tenant.Id)),
            new PricingTrialService(_fixture.CreatePricingContext(tenant.Id)),
            new PricingSnapshotSerializer());
        var waybill = await waybills.CreateWaybillAsync(new CreateWaybillCommand(
            origin.Id, dest.Id, customer.Id, WaybillPaymentType.Cash,
            "张三", "13800000000", "广州市白云区XX街1号",
            "李四", "13900000000", "武汉市汉阳区YY路2号",
            [new WaybillPackageDraft("纸箱", 2, 100m, 0.5m)],
            ruleSet.Id, DateOnly.FromDateTime(DateTime.UtcNow), OperatorId: admin.Id));
        if (confirm)
        {
            await waybills.ConfirmWaybillAsync(waybill.WaybillId, origin.Id, admin.Id);
        }

        return (tenant.Id, origin.Id, waybill.WaybillId, keeper.Id, tenantCode);
    }

    private async Task<HttpClient> KeeperClientAsync(string tenantCode)
    {
        var client = CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { tenant_code = tenantCode, username = "keeper", password = "secret-123" });
        login.EnsureSuccessStatusCode();
        var body = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("access_token").GetString());
        return client;
    }

    /// <summary>同租户内追加一个已确认未收货运单（WAREHOUSE-001 复用种子）。</summary>
    public async Task<Guid> SeedAdditionalConfirmedWaybillAsync(
        Guid tenantId, Guid originBranchId, Guid adminOperatorId)
    {
        await using var org = _fixture.CreateOrganizationContext(tenantId);
        var dest = await org.Branches.SingleAsync(b => b.Code == "WH02");
        var admin = await org.Users.SingleAsync(u => u.Username == "admin");

        Guid customerId;
        await using (var customers = _fixture.CreateCustomersContext(tenantId))
        {
            customerId = (await customers.Customers.SingleAsync(c => c.Code == "KH001")).Id;
        }

        await using var pricingDb = _fixture.CreatePricingContext(tenantId);
        var ruleSetId = (await pricingDb.RuleSets.SingleAsync(s => s.Code == "std")).Id;

        var waybills = new WaybillService(
            _fixture.CreateWaybillsContext(tenantId),
            new BranchSequenceService(_fixture.CreatePlatformContext(tenantId)),
            new PricingTrialService(_fixture.CreatePricingContext(tenantId)),
            new PricingSnapshotSerializer());
        var waybill = await waybills.CreateWaybillAsync(new CreateWaybillCommand(
            originBranchId, dest.Id, customerId, WaybillPaymentType.Cash,
            "张三", "13800000000", "广州市白云区XX街1号",
            "李四", "13900000000", "武汉市汉阳区YY路2号",
            [new WaybillPackageDraft("木架", 1, 50m, 0.3m)],
            ruleSetId, DateOnly.FromDateTime(DateTime.UtcNow), OperatorId: adminOperatorId));
        await waybills.ConfirmWaybillAsync(waybill.WaybillId, originBranchId, adminOperatorId);
        return waybill.WaybillId;
    }

    private async Task<HttpResponseMessage> PostReceiptAsync(
        HttpClient client, Guid waybillId, Guid branchId,
        int pieces = 2, decimal weight = 99.5m, decimal volume = 0.48m, string? note = null) =>
        await client.PostAsJsonAsync("/api/warehouse/receipts", new
        {
            waybillId,
            branchId,
            receivedPieces = pieces,
            receivedWeightKg = weight,
            receivedVolumeM3 = volume,
            note,
        });

    [Fact]
    public async Task RecordReceipt_ShouldTransitionStatusWriteEventAndAudit_WithDifferences()
    {
        var (tenantId, branchId, waybillId, keeperUserId, tenantCode) = await SeedConfirmedWaybillAsync();
        using var client = await KeeperClientAsync(tenantCode);

        var response = await PostReceiptAsync(client, waybillId, branchId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        // 差异：实收 2 件 = 申报 2 件；实收 99.5kg - 申报 100kg = -0.5
        Assert.Equal(0, body.RootElement.GetProperty("pieceDifference").GetInt32());
        Assert.Equal(-0.5m, body.RootElement.GetProperty("weightDifferenceKg").GetDecimal());

        // 货物状态流转 + 运单事件 + 收货记录 + 审计
        await using var waybills = _fixture.CreateWaybillsContext(tenantId);
        var waybill = await waybills.Waybills.AsNoTracking().SingleAsync(w => w.Id == waybillId);
        Assert.Equal(WaybillCargoStatus.Received, waybill.CargoStatus);
        Assert.True(await waybills.Events.AnyAsync(
            e => e.WaybillId == waybillId && e.EventType == WaybillEventType.Received));

        await using var warehouse = _fixture.CreateWarehouseContext(tenantId);
        Assert.Equal(1, await warehouse.Receipts.CountAsync(r => r.WaybillId == waybillId));

        await using var platform = _fixture.CreatePlatformContext(tenantId);
        Assert.True(await platform.AuditLogs.AnyAsync(a => a.Action == "warehouse.cargo.receive"));
        Assert.Equal(keeperUserId, (await platform.AuditLogs.SingleAsync(a => a.Action == "warehouse.cargo.receive")).ActorUserId);
    }

    [Fact]
    public async Task RecordReceipt_DraftOrCancelledWaybill_ShouldReturn409()
    {
        var (tenantId, branchId, draftWaybillId, _, tenantCode) = await SeedConfirmedWaybillAsync(confirm: false);
        using var client = await KeeperClientAsync(tenantCode);

        // 草稿运单 → 409
        var draft = await PostReceiptAsync(client, draftWaybillId, branchId);
        Assert.Equal(HttpStatusCode.Conflict, draft.StatusCode);

        // 取消运单 → 409（已确认后取消，仍不可收货）
        var adminLogin = await client.PostAsJsonAsync("/api/auth/login",
            new { tenant_code = tenantCode, username = "admin", password = "secret-123" });
        adminLogin.EnsureSuccessStatusCode();

        var waybills = new WaybillService(
            _fixture.CreateWaybillsContext(tenantId),
            new BranchSequenceService(_fixture.CreatePlatformContext(tenantId)),
            new PricingTrialService(_fixture.CreatePricingContext(tenantId)),
            new PricingSnapshotSerializer());
        await waybills.ConfirmWaybillAsync(draftWaybillId);
        await waybills.CancelWaybillAsync(draftWaybillId, "测试取消");

        var cancelled = await PostReceiptAsync(client, draftWaybillId, branchId);
        Assert.Equal(HttpStatusCode.Conflict, cancelled.StatusCode);
    }

    [Fact]
    public async Task RecordReceipt_WithoutBranchScope_ShouldReturn403()
    {
        var (tenantId, branchId, waybillId, _, tenantCode) = await SeedConfirmedWaybillAsync();

        // 收货员无 WH02 网点范围 → 对 WH02 收货 403
        await using var org = _fixture.CreateOrganizationContext(tenantId);
        var destBranch = await org.Branches.SingleAsync(b => b.Code == "WH02");
        using var client = await KeeperClientAsync(tenantCode);

        var forbidden = await PostReceiptAsync(client, waybillId, destBranch.Id);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        // 无状态变化
        await using var waybills = _fixture.CreateWaybillsContext(tenantId);
        var waybill = await waybills.Waybills.AsNoTracking().SingleAsync(w => w.Id == waybillId);
        Assert.Equal(WaybillCargoStatus.PendingReceive, waybill.CargoStatus);
    }

    [Fact]
    public async Task RecordReceipt_InvalidPieces_ShouldRollBackEverything()
    {
        var (tenantId, branchId, waybillId, _, tenantCode) = await SeedConfirmedWaybillAsync();
        using var client = await KeeperClientAsync(tenantCode);

        // 事务内收货记录违规（0 件）→ 整体回滚：状态不变、无事件、无记录、无审计
        var rollback = await PostReceiptAsync(client, waybillId, branchId, pieces: 0);
        Assert.Equal(HttpStatusCode.BadRequest, rollback.StatusCode);

        await using var waybills = _fixture.CreateWaybillsContext(tenantId);
        var waybill = await waybills.Waybills.AsNoTracking().SingleAsync(w => w.Id == waybillId);
        Assert.Equal(WaybillCargoStatus.PendingReceive, waybill.CargoStatus);
        Assert.False(await waybills.Events.AnyAsync(e => e.WaybillId == waybillId && e.EventType == WaybillEventType.Received));

        await using var warehouse = _fixture.CreateWarehouseContext(tenantId);
        Assert.Equal(0, await warehouse.Receipts.CountAsync());
    }

    [Fact]
    public async Task RecordReceipt_BatchReceipts_ShouldAppendWithoutStatusChange()
    {
        var (tenantId, branchId, waybillId, _, tenantCode) = await SeedConfirmedWaybillAsync();
        using var client = await KeeperClientAsync(tenantCode);

        await PostReceiptAsync(client, waybillId, branchId);
        var second = await PostReceiptAsync(client, waybillId, branchId, pieces: 1, weight: 20m, volume: 0.1m);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        await using var waybills = _fixture.CreateWaybillsContext(tenantId);
        var waybill = await waybills.Waybills.AsNoTracking().SingleAsync(w => w.Id == waybillId);
        Assert.Equal(WaybillCargoStatus.Received, waybill.CargoStatus); // 不再变状态
        Assert.Equal(1, await waybills.Events.CountAsync(
            e => e.WaybillId == waybillId && e.EventType == WaybillEventType.Received)); // 仅首次

        await using var warehouse = _fixture.CreateWarehouseContext(tenantId);
        Assert.Equal(2, await warehouse.Receipts.CountAsync(r => r.WaybillId == waybillId));
    }

    [Fact]
    public async Task Receipts_ForOtherTenantWaybill_ShouldReturn404()
    {
        var (_, branchId, waybillId, _, tenantCode) = await SeedConfirmedWaybillAsync();
        using var client = await KeeperClientAsync(tenantCode);

        var otherTenant = Tenant.Create($"rcpt-x-{Guid.NewGuid():N}", "其他租户", DateTime.UtcNow);
        await using (var tenancy = _fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(otherTenant);
            await tenancy.SaveChangesAsync();
        }

        await using var otherOrg = _fixture.CreateOrganizationContext(otherTenant.Id);
        var otherKeeper = User.Create(
            otherTenant.Id, "keeper", "收货员", PasswordHasher.Hash("secret-123"), DateTime.UtcNow);
        var otherRole = Role.Create(otherTenant.Id, "keeper", "仓储员", DateTime.UtcNow);
        otherOrg.Users.Add(otherKeeper);
        otherOrg.Roles.Add(otherRole);
        await otherOrg.SaveChangesAsync();
        var otherAuthorization = new AuthorizationService(otherOrg);
        await otherAuthorization.GrantPermissionAsync(otherRole.Id, WarehousePermissionCodes.CargoReceive);
        await otherAuthorization.AssignRoleAsync(otherKeeper.Id, otherRole.Id);

        // 他租户自己的网点（复合外键：不能引用租户甲的网点）
        var otherBranch = Branch.Create(otherTenant.Id, "GZ01", "他租户广州", DateTime.UtcNow);
        otherOrg.Branches.Add(otherBranch);
        await otherOrg.SaveChangesAsync();
        await otherAuthorization.GrantBranchScopeAsync(otherKeeper.Id, otherBranch.Id);

        using var otherClient = CreateClient();
        var login = await otherClient.PostAsJsonAsync("/api/auth/login",
            new { tenant_code = otherTenant.Code, username = "keeper", password = "secret-123" });
        login.EnsureSuccessStatusCode();
        var body = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("access_token").GetString());

        // 授权正常但运单不可见（跨租户）→ 404
        var notFound = await PostReceiptAsync(otherClient, waybillId, otherBranch.Id);
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
    }
}
