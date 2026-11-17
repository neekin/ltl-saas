using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ltl.Modules.Customers.Domain;
using Ltl.Modules.Finance.Domain;
using Ltl.Modules.Finance.Application;
using Ltl.Modules.Finance.Infrastructure;
using Ltl.Modules.Platform.Application;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Platform.Application;
using Ltl.Modules.Pricing.Application;
using Ltl.Modules.Pricing.Domain;
using Ltl.Modules.Tenancy.Domain;
using Ltl.Modules.Waybills.Application;
using Ltl.Modules.Waybills.Domain;
using Ltl.SharedKernel.Application;
using Ltl.SharedKernel.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ltl.IntegrationTests.Finance;

/// <summary>FIN-001 集成测试：应收自动创建、部分/全额收款、退款、签收不等于结清。</summary>
public class FinanceServiceTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public FinanceServiceTests(PostgreSqlFixture fixture)
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

    private async Task<FinanceSeed> SeedAsync()
    {
        var tenantCode = $"fin-{Guid.NewGuid():N}";
        var tenant = Tenant.Create(tenantCode, "财务测试租户", DateTime.UtcNow);
        await using (var tenancy = _fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenant);
            await tenancy.SaveChangesAsync();
        }

        await using var org = _fixture.CreateOrganizationContext(tenant.Id);
        var origin = Branch.Create(tenant.Id, "GZ01", "广州", DateTime.UtcNow);
        var dest = Branch.Create(tenant.Id, "WH02", "武汉", DateTime.UtcNow);
        var admin = User.Create(tenant.Id, "admin", "管理员", PasswordHasher.Hash("secret-123"), DateTime.UtcNow);
        var role = Role.Create(tenant.Id, "ops", "管理员", DateTime.UtcNow);
        org.Branches.AddRange(origin, dest);
        org.Users.Add(admin);
        org.Roles.Add(role);
        await org.SaveChangesAsync();

        await using var customers = _fixture.CreateCustomersContext(tenant.Id);
        var customer = Customer.Create(tenant.Id, "KH001", "测试客户", DateTime.UtcNow);
        customers.Customers.Add(customer);
        await customers.SaveChangesAsync();

        var authorization = new AuthorizationService(org);
        await authorization.GrantPermissionAsync(role.Id, FinancePermissionCodes.FinanceManage);
        await authorization.AssignRoleAsync(admin.Id, role.Id);
        await authorization.GrantBranchScopeAsync(admin.Id, origin.Id);

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
                        Name = "运费", Type = PricingFreightType.ByWeight, UnitPrice = 10m,
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
            "张三", "138", "广州", "李四", "139", "武汉",
            [new WaybillPackageDraft("纸箱", 1, 100m, 0.5m)],
            ruleSet.Id, DateOnly.FromDateTime(DateTime.UtcNow), OperatorId: admin.Id));
        await waybills.ConfirmWaybillAsync(waybill.WaybillId, origin.Id, admin.Id);

        return new FinanceSeed(tenant.Id, waybill.WaybillId, tenantCode, waybill.TotalAmount);
    }

    public sealed record FinanceSeed(
        Guid TenantId, Guid WaybillId, string TenantCode, decimal TotalAmount);

    private async Task<HttpClient> AdminClientAsync(string tenantCode)
    {
        var client = CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { tenant_code = tenantCode, username = "admin", password = "secret-123" });
        login.EnsureSuccessStatusCode();
        var body = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("access_token").GetString());
        return client;
    }

    [Fact]
    public async Task Payment_FullFlow_ShouldTrackReceivableCorrectly()
    {
        var seed = await SeedAsync();
        using var client = await AdminClientAsync(seed.TenantCode);

        // 首次查询应收（懒创建）→ Unpaid
        var ensure = await client.PostAsJsonAsync("/api/finance/payments", new
        {
            waybillId = seed.WaybillId, amount = seed.TotalAmount / 2, method = "cash", note = (string?)null,
        });
        // 第一笔收款会先懒创建应收（金额 = 运单 TotalAmount = 1000），然后收款 500
        Assert.Equal(HttpStatusCode.OK, ensure.StatusCode);

        // 查应收
        var receivable = await client.GetAsync($"/api/finance/receivables?waybillId={seed.WaybillId}");
        Assert.Equal(HttpStatusCode.OK, receivable.StatusCode);
        var rvBody = await JsonDocument.ParseAsync(await receivable.Content.ReadAsStreamAsync());
        Assert.Equal("PartiallyPaid", rvBody.RootElement.GetProperty("status").GetString());

        // 第二笔收款（剩余金额）→ Settled
        var remaining = seed.TotalAmount - (seed.TotalAmount / 2);
        var payment2 = await client.PostAsJsonAsync("/api/finance/payments", new
        {
            waybillId = seed.WaybillId, amount = remaining, method = "cash", note = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, payment2.StatusCode);

        var rv2 = await client.GetAsync($"/api/finance/receivables?waybillId={seed.WaybillId}");
        var rv2Body = await JsonDocument.ParseAsync(await rv2.Content.ReadAsStreamAsync());
        Assert.Equal("Settled", rv2Body.RootElement.GetProperty("status").GetString());
        Assert.Equal(seed.TotalAmount, rv2Body.RootElement.GetProperty("settledAmount").GetDecimal());
    }

    [Fact]
    public async Task Overpayment_ShouldReturn409()
    {
        var seed = await SeedAsync();
        using var client = await AdminClientAsync(seed.TenantCode);

        var response = await client.PostAsJsonAsync("/api/finance/payments", new
        {
            waybillId = seed.WaybillId,
            amount = seed.TotalAmount + 1,
            method = "cash",
            note = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}

/// <summary>月结账单生命周期 + COD 退款 + 信用额度测试（服务级，直接构造）。</summary>
public class StatementAndCodTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public StatementAndCodTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(Guid TenantId, Guid CustomerId, Guid WaybillId, decimal TotalAmount)> SeedAsync()
    {
        var tenantCode = $"stmt-{Guid.NewGuid():N}";
        var tenant = Tenant.Create(tenantCode, "月结测试租户", DateTime.UtcNow);
        await using (var tenancy = _fixture.CreateTenancyContext())
        {
            tenancy.Tenants.Add(tenant);
            await tenancy.SaveChangesAsync();
        }

        await using var org = _fixture.CreateOrganizationContext(tenant.Id);
        var origin = Branch.Create(tenant.Id, "GZ01", "广州", DateTime.UtcNow);
        var dest = Branch.Create(tenant.Id, "WH02", "武汉", DateTime.UtcNow);
        org.Branches.AddRange(origin, dest);
        await org.SaveChangesAsync();

        await using var customers = _fixture.CreateCustomersContext(tenant.Id);
        var customer = Customer.Create(tenant.Id, "KH001", "测试客户", DateTime.UtcNow);
        customers.Customers.Add(customer);
        await customers.SaveChangesAsync();

        var pricing = new PricingRuleSetService(_fixture.CreatePricingContext(tenant.Id));
        var ruleSet = await pricing.CreateRuleSetAsync("std", "标准价卡", DateTime.UtcNow);
        var version = await pricing.CreateDraftVersionAsync(ruleSet.Id, new PricingRuleDefinition
        {
            Rounding = RoundingPolicy.HalfUpTwoDecimals,
            Rules = [
                new PricingRule
                {
                    Priority = 1,
                    Name = "标准运价",
                    Enabled = true,
                    BillingWeightStrategy = BillingWeightStrategy.WeightOnly,
                    Conditions = [],
                    Freight = new PricingFreightFeeItem
                    {
                        Name = "运费", Type = PricingFreightType.ByWeight, UnitPrice = 10m,
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
            "张三", "138", "广州", "李四", "139", "武汉",
            [new WaybillPackageDraft("纸箱", 1, 100m, 0.5m)],
            ruleSet.Id, DateOnly.FromDateTime(DateTime.UtcNow)));
        await waybills.ConfirmWaybillAsync(waybill.WaybillId, origin.Id, null);

        return (tenant.Id, customer.Id, waybill.WaybillId, waybill.TotalAmount);
    }

    private FinanceService CreateFinanceService(Guid tenantId)
    {
        var db = _fixture.CreateFinanceContext(tenantId);
        var audit = new AuditLogService(_fixture.CreatePlatformContext(tenantId));
        var authorization = new AuthorizationService(_fixture.CreateOrganizationContext(tenantId));
        return new FinanceService(db, authorization, audit);
    }

    [Fact]
    public async Task MonthlyStatement_FullLifecycle_ShouldWork()
    {
        var (tenantId, customerId, waybillId, totalAmount) = await SeedAsync();
        var service = CreateFinanceService(tenantId);

        // 确保应收存在
        await service.EnsureReceivableAsync(waybillId);

        // 生成月结账单 → Draft
        var statement = await service.GenerateStatementAsync(Guid.NewGuid(), customerId, "202611");
        Assert.Equal(CustomerStatementStatus.Draft, statement.Status);

        // 重复生成 → 409
        await Assert.ThrowsAsync<ConflictException>(
            () => service.GenerateStatementAsync(Guid.NewGuid(), customerId, "202611"));

        // 确认 → Confirmed
        await service.ConfirmStatementAsync(Guid.NewGuid(), statement.Id);

        // 关闭 → Closed
        await service.CloseStatementAsync(Guid.NewGuid(), statement.Id);

        // 已关闭再确认 → 409
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ConfirmStatementAsync(Guid.NewGuid(), statement.Id));
    }

    [Fact]
    public async Task CodRefund_ShouldReverseCollection()
    {
        var (tenantId, customerId, waybillId, _) = await SeedAsync();
        var service = CreateFinanceService(tenantId);

        // 创建 COD 订单（需运单有 COD 金额）——用非零 COD 运单
        // 此测试用简化：直接通过 finance context 手动创建 COD 订单
        var codOrder = CodOrder.Create(tenantId, waybillId, 500m, DateTime.UtcNow);
        await using (var context = _fixture.CreateFinanceContext(tenantId))
        {
            context.CodOrders.Add(codOrder);
            await context.SaveChangesAsync();
        }

        var codService = new CodService(
            _fixture.CreateFinanceContext(tenantId),
            new AuthorizationService(_fixture.CreateOrganizationContext(tenantId)),
            new AuditLogService(_fixture.CreatePlatformContext(tenantId)));

        // COD 收款 300
        await codService.RecordCollectionAsync(
            Guid.NewGuid(), new RecordCodCollectionRequest(waybillId, 300m, null));

        // 验证收款状态
        await using var verify = _fixture.CreateFinanceContext(tenantId);
        var order = await verify.CodOrders.SingleAsync(o => o.Id == codOrder.Id);
        Assert.Equal(300m, order.CollectedAmount);

        // 退款 200
        var collection = await verify.CodCollections.SingleAsync(c => c.CodOrderId == codOrder.Id);
        var refund = await codService.RefundCollectionAsync(
            Guid.NewGuid(), collection.Id, 200m, "测试退款");

        // 验证回退
        await using var verify2 = _fixture.CreateFinanceContext(tenantId);
        var order2 = await verify2.CodOrders.AsNoTracking().SingleAsync(o => o.Id == codOrder.Id);
        Assert.Equal(100m, order2.CollectedAmount);

        // 超额退款 → 409
        await Assert.ThrowsAsync<ConflictException>(
            () => codService.RefundCollectionAsync(Guid.NewGuid(), collection.Id, 999m, "超额"));
    }

    [Fact]
    public async Task CreditLimit_WithSetLimit_ShouldCheckOutstanding()
    {
        var (tenantId, customerId, waybillId, totalAmount) = await SeedAsync();
        var service = CreateFinanceService(tenantId);
        await service.EnsureReceivableAsync(waybillId);

        // 设置信用额度 = 应收金额 → 在额度内
        await using var customers = _fixture.CreateCustomersContext(tenantId);
        var customer = await customers.Customers.SingleAsync(c => c.Id == customerId);
        customer.SetCreditPolicy(totalAmount, 30, DateTime.UtcNow);
        await customers.SaveChangesAsync();

        var withinLimit = await service.IsWithinCreditLimitAsync(customerId);
        Assert.True(withinLimit);

        // 设置信用额度 = 应收金额一半 → 超出
        customer.SetCreditPolicy(totalAmount / 2, 30, DateTime.UtcNow);
        await customers.SaveChangesAsync();

        var exceeded = await service.IsWithinCreditLimitAsync(customerId);
        Assert.False(exceeded);
    }
}
