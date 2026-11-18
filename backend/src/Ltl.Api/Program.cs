using System.Security.Claims;
using System.Text;
using Ltl.Api.Infrastructure;
using Ltl.Modules.Customers.Application;
using Ltl.Modules.Customers.Infrastructure;
using Ltl.Modules.Organization.Application.Admin;
using Ltl.Modules.Organization.Application.Auth;
using Ltl.Modules.Reporting.Application;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Organization.Infrastructure;
using Ltl.Modules.Pricing.Application;
using Ltl.Modules.Pricing.Infrastructure;
using Ltl.Api.Endpoints;
using Ltl.Modules.Platform.Application;
using Ltl.Modules.Platform.Application;
using Ltl.Modules.Platform.Infrastructure;
using Ltl.Modules.Routing.Application;
using Ltl.Modules.Routing.Infrastructure;
using Ltl.Modules.Tenancy.Infrastructure;
using Ltl.Modules.Warehouse.Application;
using Ltl.Modules.Transport.Application;
using Ltl.Modules.Transport.Infrastructure;
using Ltl.Modules.Delivery.Application;
using Ltl.Modules.Finance.Application;
using Ltl.Modules.Finance.Infrastructure;
using Ltl.Modules.Delivery.Infrastructure;
using Ltl.Modules.Arrivals.Application;
using Ltl.Modules.Arrivals.Infrastructure;
using Ltl.Modules.Warehouse.Infrastructure;
using Ltl.Modules.Waybills.Application;
using Ltl.Modules.Waybills.Infrastructure;
using Ltl.SharedKernel.Application;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 结构化日志：Serilog（P0 技术基线），配置来自 appsettings 的 "Serilog" 节
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddHealthChecks();

// Hangfire 后台任务（PostgreSQL 存储）
builder.Services.AddHangfire(config => config
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(builder.Configuration.GetConnectionString("Tenancy")))
    .UseSerilogLogProvider());
builder.Services.AddHangfireServer();
builder.Services.AddOpenApi();
// 枚举响应统一字符串（前端可读；请求侧 camelCase 由 Web 默认处理）
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
// 特定异常处理器必须注册在兜底 GlobalExceptionHandler 之前（IExceptionHandler 按注册顺序匹配）
builder.Services.AddExceptionHandler<TenantContextMissingExceptionHandler>();
builder.Services.AddExceptionHandler<AuthenticationFailedExceptionHandler>();
builder.Services.AddExceptionHandler<PermissionDeniedExceptionHandler>();
builder.Services.AddExceptionHandler<EntityNotFoundExceptionHandler>();
builder.Services.AddExceptionHandler<ConflictExceptionHandler>();
builder.Services.AddExceptionHandler<ArgumentValidationExceptionHandler>();
builder.Services.AddExceptionHandler<WaybillDomainViolationHandler>();
builder.Services.AddExceptionHandler<PricingRuleViolationHandler>();
builder.Services.AddExceptionHandler<WarehouseDomainViolationHandler>();
builder.Services.AddExceptionHandler<TransportDomainViolationHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// JWT 认证（AUTH-001；契约 ADR-0005）：tenant_id 只来自已验证 token 的 claim（AGENTS §5.1）
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
builder.Services.Configure<JwtOptions>(jwtSection);
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = jwtSection.Get<JwtOptions>() ?? throw new InvalidOperationException("缺少 Jwt 配置节。");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
// 业务 API 默认要求认证；匿名端点显式 AllowAnonymous（AUTH-001 设计 §3）。
// AuthorizationPolicyBuilder 全限定：避免与 Organization 模块的 IAuthorizationService 命名冲突
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

// Tenancy 模块（TEN-001）
builder.Services.AddDbContext<TenancyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Tenancy"))
        .UseSnakeCaseNamingConvention());

// Organization 模块（ORG-001/002；AUTH-001 登录与刷新令牌）
builder.Services.AddDbContext<OrganizationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Tenancy"))
        .UseSnakeCaseNamingConvention());

// Customers 模块（CUS-001）
builder.Services.AddDbContext<CustomersDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Tenancy"))
        .UseSnakeCaseNamingConvention());
builder.Services.AddScoped<ICustomerRelationshipService, CustomerRelationshipService>();
builder.Services.AddScoped<ICustomerAdminService, CustomerAdminService>();
builder.Services.AddScoped<IRouteAdminService, RouteAdminService>();

// Routing 模块（ROUTE-001）
builder.Services.AddDbContext<RoutingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Tenancy"))
        .UseSnakeCaseNamingConvention());

// Platform 模块（SEQ-001：序号分配）
builder.Services.AddDbContext<PlatformDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Tenancy"))
        .UseSnakeCaseNamingConvention());
builder.Services.AddScoped<IBranchSequenceService, BranchSequenceService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
// 文件存储（FILE-001）：抽象接口 + 本地开发实现；生产 S3 适配器随 P9 接入
builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection(FileStorageOptions.SectionName));
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddScoped<IFileObjectService, FileObjectService>();

// Pricing 模块（PRICE-001/002：计价规则模型、版本管理、执行器与试算）
builder.Services.AddDbContext<PricingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Tenancy"))
        .UseSnakeCaseNamingConvention());
builder.Services.AddScoped<IPricingRuleSetService, PricingRuleSetService>();
builder.Services.AddScoped<IPricingTrialService, PricingTrialService>();
builder.Services.AddScoped<IPricingSnapshotSerializer, PricingSnapshotSerializer>();

// Waybills 模块（WB-001/002：运单模型、开单、快照与事件）
builder.Services.AddDbContext<WaybillsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Tenancy"))
        .UseSnakeCaseNamingConvention());
builder.Services.AddScoped<IWaybillService, WaybillService>();
builder.Services.AddScoped<IWaybillCargoService, WaybillCargoService>();
builder.Services.AddScoped<IWaybillQueryCodeService, WaybillQueryCodeService>();

// Warehouse 模块（RECEIVE-001：收货登记、称重、体积复核）
builder.Services.AddDbContext<WarehouseDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Tenancy"))
        .UseSnakeCaseNamingConvention());
builder.Services.AddScoped<ICargoReceiptService, CargoReceiptService>();
builder.Services.AddScoped<ICargoSortingService, CargoSortingService>();
builder.Services.AddScoped<IBranchTransferService, BranchTransferService>();

// Arrival 模块（ARRIVAL-001：到货登记）
builder.Services.AddDbContext<ArrivalDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Tenancy"))
        .UseSnakeCaseNamingConvention());
builder.Services.AddScoped<IArrivalService, ArrivalService>();

// Delivery 模块（DELIVERY-001：交付/签收/拒收）
builder.Services.AddDbContext<DeliveryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Tenancy"))
        .UseSnakeCaseNamingConvention());
builder.Services.AddScoped<IDeliveryService, DeliveryService>();

// Finance 模块（FIN-001：应收/收款/核销/退款）
builder.Services.AddDbContext<FinanceDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Tenancy"))
        .UseSnakeCaseNamingConvention());
builder.Services.AddScoped<IFinanceService, FinanceService>();
builder.Services.AddScoped<ICodService, CodService>();

// Reporting 模块（REPORT-001：经营看板）
builder.Services.AddScoped<IReportingService, ReportingService>();

// Transport 模块（VEHICLE-001：车辆/司机档案）
builder.Services.AddDbContext<TransportDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Tenancy"))
        .UseSnakeCaseNamingConvention());
builder.Services.AddScoped<IFleetAdminService, FleetAdminService>();
builder.Services.AddScoped<ITripService, TripService>();
builder.Services.AddScoped<IWaybillPrintService, WaybillPrintService>();
builder.Services.AddScoped<IWaybillAdminService, WaybillAdminService>();

builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<IAuthorizationService, AuthorizationService>();
builder.Services.AddScoped<IJwtTokenIssuer, JwtTokenIssuer>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBranchAdminService, BranchAdminService>();
builder.Services.AddScoped<IUserAdminService, UserAdminService>();
builder.Services.AddScoped<IRoleAdminService, RoleAdminService>();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
// ADR-0005 顺序：UseAuthentication → TenantResolutionMiddleware（从认证身份解析租户）
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { name = "LTL SaaS API", status = "ok" }))
    .AllowAnonymous();
app.MapHealthChecks("/health")
    .AllowAnonymous();

// ===== 认证端点（AUTH-001，匿名）=====
app.MapPost("/api/auth/login", async (LoginRequest request, IAuthService authService) =>
    {
        var result = await authService.LoginAsync(request.TenantCode, request.Username, request.Password);
        return Results.Ok(new
        {
            access_token = result.AccessToken,
            expires_in = result.ExpiresInSeconds,
            refresh_token = result.RefreshToken,
            user = new
            {
                id = result.UserId,
                username = result.Username,
                displayName = result.DisplayName,
                tenantId = result.TenantId,
                permissions = result.Permissions,
            },
        });
    })
    .AllowAnonymous();

app.MapPost("/api/auth/refresh", async (RefreshRequest request, IAuthService authService) =>
    {
        var result = await authService.RefreshAsync(request.RefreshToken);
        return Results.Ok(new
        {
            access_token = result.AccessToken,
            expires_in = result.ExpiresInSeconds,
            refresh_token = result.RefreshToken,
        });
    })
    .AllowAnonymous();

app.MapPost("/api/auth/logout", async (RefreshRequest request, IAuthService authService) =>
    {
        await authService.LogoutAsync(request.RefreshToken);
        return Results.Ok(new { revoked = true });
    })
    .AllowAnonymous();

// 当前用户（认证）：实时权限点，供前端菜单/按钮展示；后端授权始终实时查库
app.MapGet("/api/auth/me", async (HttpContext context, IAuthService authService) =>
    {
        var userIdText = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new AuthenticationFailedException("无效的认证凭据");
        var (userId, username, displayName, tenantId, permissions) =
            await authService.GetCurrentUserAsync(Guid.Parse(userIdText));
        return Results.Ok(new
        {
            userId,
            username,
            displayName,
            tenantId,
            permissions,
        });
    });

app.MapOrganizationEndpoints();
app.MapFileEndpoints();
app.MapCustomerEndpoints();
app.MapRouteEndpoints();
app.MapPricingEndpoints();
app.MapPricingTrialEndpoint();
app.MapWarehouseEndpoints();
app.MapWaybillEndpoints();
app.MapTransportEndpoints();
app.MapArrivalEndpoints();
app.MapDeliveryEndpoints();
app.MapFinanceEndpoints();
app.MapCodEndpoints();
app.MapReportingEndpoints();

// 诊断端点：返回服务端解析出的租户上下文（TEN-001 设计文档第 7 节）；
// 现受 FallbackPolicy 保护：未认证 → 401（认证后返回 claim 解析的租户）
app.MapGet("/api/tenancy/current", (ITenantContext tenantContext) =>
    Results.Ok(new { tenantId = tenantContext.RequireTenant() }));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();

    // 开发环境启动时应用迁移（生产由部署流程执行迁移，P9）
    using (var scope = app.Services.CreateScope())
    {
        scope.ServiceProvider.GetRequiredService<TenancyDbContext>().Database.Migrate();
        scope.ServiceProvider.GetRequiredService<OrganizationDbContext>().Database.Migrate();
        scope.ServiceProvider.GetRequiredService<CustomersDbContext>().Database.Migrate();
        scope.ServiceProvider.GetRequiredService<RoutingDbContext>().Database.Migrate();
        scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.Migrate();
        scope.ServiceProvider.GetRequiredService<PricingDbContext>().Database.Migrate();
        scope.ServiceProvider.GetRequiredService<WaybillsDbContext>().Database.Migrate();
        scope.ServiceProvider.GetRequiredService<WarehouseDbContext>().Database.Migrate();
        scope.ServiceProvider.GetRequiredService<TransportDbContext>().Database.Migrate();
        scope.ServiceProvider.GetRequiredService<ArrivalDbContext>().Database.Migrate();
        scope.ServiceProvider.GetRequiredService<DeliveryDbContext>().Database.Migrate();
        scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Database.Migrate();
    }

    // Hangfire Dashboard（仅开发环境）
    app.UseHangfireDashboard("/hangfire");

    // 周期任务：清理过期刷新令牌（每日）
    RecurringJob.AddOrUpdate<HangfireJobs>(
        "cleanup-expired-tokens",
        j => j.CleanupExpiredTokensAsync(),
        Cron.Daily);

    // 周期任务：逾期应收检查（每日）
    RecurringJob.AddOrUpdate<HangfireJobs>(
        "overdue-receivables-check",
        j => j.CheckOverdueReceivablesAsync(),
        Cron.Daily);

    // 周期任务：到货超时提醒（每日）
    RecurringJob.AddOrUpdate<HangfireJobs>(
        "arrival-timeout-alert",
        j => j.CheckArrivalTimeoutAsync(),
        Cron.Daily);

    // 开发环境种子数据：幂等（已存在则跳过）
    SeedDevelopmentData(app.Services);

    // 供本地调试与集成测试验证统一异常处理；仅开发环境暴露
    app.MapGet("/dev/throw", () =>
    {
        throw new InvalidOperationException("演示异常：验证统一 ProblemDetails 错误格式");
    }).AllowAnonymous();
}

app.Run();

// ===== 开发环境种子数据（幂等） =====
static void SeedDevelopmentData(IServiceProvider services)
{
    var scope = services.CreateScope();
    try
    {
        var tenancyDb = scope.ServiceProvider.GetRequiredService<Ltl.Modules.Tenancy.Infrastructure.TenancyDbContext>();
        var orgDb = scope.ServiceProvider.GetRequiredService<Ltl.Modules.Organization.Infrastructure.OrganizationDbContext>();
        var customersDb = scope.ServiceProvider.GetRequiredService<Ltl.Modules.Customers.Infrastructure.CustomersDbContext>();
        var pricingDb = scope.ServiceProvider.GetRequiredService<Ltl.Modules.Pricing.Infrastructure.PricingDbContext>();

        // 1. 租户
        var tenant = tenancyDb.Tenants.FirstOrDefault(t => t.Code == "demo");
        if (tenant is null)
        {
            tenant = Ltl.Modules.Tenancy.Domain.Tenant.Create("demo", "演示物流公司", DateTime.UtcNow);
            tenancyDb.Tenants.Add(tenant);
            tenancyDb.SaveChanges();
        }

        // 2. 网点
        if (!orgDb.Branches.Any(b => b.Code == "GZ01"))
        {
            orgDb.Branches.Add(Ltl.Modules.Organization.Domain.Branch.Create(tenant.Id, "GZ01", "广州", DateTime.UtcNow));
        }
        if (!orgDb.Branches.Any(b => b.Code == "WH02"))
        {
            orgDb.Branches.Add(Ltl.Modules.Organization.Domain.Branch.Create(tenant.Id, "WH02", "武汉", DateTime.UtcNow));
        }
        orgDb.SaveChanges();

        // 3. 角色 + 权限
        var adminRole = orgDb.Roles.FirstOrDefault(r => r.Code == "admin");
        if (adminRole is null)
        {
            adminRole = Ltl.Modules.Organization.Domain.Role.Create(tenant.Id, "admin", "管理员", DateTime.UtcNow);
            orgDb.Roles.Add(adminRole);
            orgDb.SaveChanges();
        }

        // 授予全部权限
        foreach (var permission in orgDb.Permissions.ToList())
        {
            var exists = orgDb.RolePermissions.Any(rp => rp.RoleId == adminRole.Id && rp.PermissionId == permission.Id);
            if (!exists)
            {
                orgDb.RolePermissions.Add(Ltl.Modules.Organization.Domain.RolePermission.Create(
                    tenant.Id, adminRole.Id, permission.Id));
            }
        }
        orgDb.SaveChanges();

        // 4. 用户
        var adminUser = orgDb.Users.FirstOrDefault(u => u.Username == "admin");
        if (adminUser is null)
        {
            adminUser = Ltl.Modules.Organization.Domain.User.Create(
                tenant.Id, "admin", "管理员",
                Ltl.SharedKernel.Security.PasswordHasher.Hash("Admin@123"),
                DateTime.UtcNow);
            orgDb.Users.Add(adminUser);
            orgDb.SaveChanges();
        }

        var userRoleExists = orgDb.UserRoles.Any(ur => ur.UserId == adminUser.Id && ur.RoleId == adminRole.Id);
        if (!userRoleExists)
        {
            orgDb.UserRoles.Add(Ltl.Modules.Organization.Domain.UserRole.Create(tenant.Id, adminUser.Id, adminRole.Id));
            orgDb.SaveChanges();
        }

        // 网点范围（租户级）
        if (!adminUser.TenantWideScope)
        {
            adminUser.GrantTenantWideScope(DateTime.UtcNow);
            orgDb.SaveChanges();
        }

        // 5. 客户
        var customer = customersDb.Customers.FirstOrDefault(c => c.Code == "KH001");
        if (customer is null)
        {
            customer = Ltl.Modules.Customers.Domain.Customer.Create(
                tenant.Id, "KH001", "演示客户", DateTime.UtcNow);
            customersDb.Customers.Add(customer);
            customersDb.SaveChanges();
        }

        // 6. 已发布价卡
        var ruleSet = pricingDb.RuleSets.FirstOrDefault(s => s.Code == "std");
        if (ruleSet is null)
        {
            ruleSet = Ltl.Modules.Pricing.Domain.PricingRuleSet.Create(
                tenant.Id, "std", "标准价卡", DateTime.UtcNow);
            pricingDb.RuleSets.Add(ruleSet);
            pricingDb.SaveChanges();

            var version = Ltl.Modules.Pricing.Domain.PricingRuleSetVersion.CreateDraft(
                tenant.Id, ruleSet.Id, 1,
                new Ltl.Modules.Pricing.Domain.PricingRuleDefinition
                {
                    Rounding = Ltl.Modules.Pricing.Domain.RoundingPolicy.HalfUpTwoDecimals,
                    Rules =
                    [
                        new Ltl.Modules.Pricing.Domain.PricingRule
                        {
                            Priority = 1,
                            Name = "标准运价",
                            Enabled = true,
                            BillingWeightStrategy = Ltl.Modules.Pricing.Domain.BillingWeightStrategy.WeightOnly,
                            Conditions = [],
                            Freight = new Ltl.Modules.Pricing.Domain.PricingFreightFeeItem
                            {
                                Name = "运费",
                                Type = Ltl.Modules.Pricing.Domain.PricingFreightType.ByWeight,
                                UnitPrice = 1.5m,
                            },
                            Surcharges = [],
                        },
                    ],
                },
                DateTime.UtcNow);
            pricingDb.RuleSetVersions.Add(version);
            pricingDb.SaveChanges();

            version.Publish(DateTime.UtcNow, adminUser.Id);
            pricingDb.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Seed] 种子数据创建失败：{ex.Message}");
    }
    finally
    {
        scope.Dispose();
    }
}

// 供 WebApplicationFactory 集成测试作为入口类型
public partial class Program;

/// <summary>
/// 认证请求契约采用 OAuth 惯例 snake_case（显式 JsonPropertyName，避免 camelCase 匹配歧义）。
/// </summary>
public sealed record LoginRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("tenant_code")] string TenantCode,
    [property: System.Text.Json.Serialization.JsonPropertyName("username")] string Username,
    [property: System.Text.Json.Serialization.JsonPropertyName("password")] string Password);

public sealed record RefreshRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("refresh_token")] string RefreshToken);
