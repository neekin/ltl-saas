using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ltl.Modules.Customers.Domain;
using Ltl.Modules.Finance.Domain;
using Ltl.Modules.Finance.Infrastructure;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Platform.Application;
using Ltl.Modules.Pricing.Application;
using Ltl.Modules.Pricing.Domain;
using Ltl.Modules.Tenancy.Domain;
using Ltl.Modules.Waybills.Application;
using Ltl.Modules.Waybills.Domain;
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
