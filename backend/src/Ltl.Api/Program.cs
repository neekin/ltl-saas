using Ltl.Api.Infrastructure;
using Ltl.Modules.Customers.Application;
using Ltl.Modules.Customers.Infrastructure;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Organization.Infrastructure;
using Ltl.Modules.Platform.Application;
using Ltl.Modules.Platform.Infrastructure;
using Ltl.Modules.Routing.Infrastructure;
using Ltl.Modules.Tenancy.Infrastructure;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 结构化日志：Serilog（P0 技术基线），配置来自 appsettings 的 "Serilog" 节
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
// 特定异常处理器必须注册在兜底 GlobalExceptionHandler 之前（IExceptionHandler 按注册顺序匹配）
builder.Services.AddExceptionHandler<TenantContextMissingExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Tenancy 模块（TEN-001）
builder.Services.AddDbContext<TenancyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Tenancy"))
        .UseSnakeCaseNamingConvention());

// Organization 模块（ORG-001）
builder.Services.AddDbContext<OrganizationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Tenancy"))
        .UseSnakeCaseNamingConvention());

// Customers 模块（CUS-001）
builder.Services.AddDbContext<CustomersDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Tenancy"))
        .UseSnakeCaseNamingConvention());
builder.Services.AddScoped<ICustomerRelationshipService, CustomerRelationshipService>();

// Routing 模块（ROUTE-001）
builder.Services.AddDbContext<RoutingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Tenancy"))
        .UseSnakeCaseNamingConvention());

// Platform 模块（SEQ-001：序号分配）
builder.Services.AddDbContext<PlatformDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Tenancy"))
        .UseSnakeCaseNamingConvention());
builder.Services.AddScoped<IBranchSequenceService, BranchSequenceService>();

builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<IAuthorizationService, AuthorizationService>();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapGet("/", () => Results.Ok(new { name = "LTL SaaS API", status = "ok" }));
app.MapHealthChecks("/health");

// 诊断端点：返回服务端解析出的租户上下文（TEN-001 设计文档第 7 节）
app.MapGet("/api/tenancy/current", (ITenantContext tenantContext) =>
    Results.Ok(new { tenantId = tenantContext.RequireTenant() }));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // 开发环境启动时应用迁移（生产由部署流程执行迁移，P9）
    using (var scope = app.Services.CreateScope())
    {
        scope.ServiceProvider.GetRequiredService<TenancyDbContext>().Database.Migrate();
        scope.ServiceProvider.GetRequiredService<OrganizationDbContext>().Database.Migrate();
        scope.ServiceProvider.GetRequiredService<CustomersDbContext>().Database.Migrate();
        scope.ServiceProvider.GetRequiredService<RoutingDbContext>().Database.Migrate();
        scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.Migrate();
    }

    // 供本地调试与集成测试验证统一异常处理；仅开发环境暴露
    app.MapGet("/dev/throw", () =>
    {
        throw new InvalidOperationException("演示异常：验证统一 ProblemDetails 错误格式");
    });
}

app.Run();

// 供 WebApplicationFactory 集成测试作为入口类型
public partial class Program;
