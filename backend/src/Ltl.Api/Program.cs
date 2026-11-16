using Ltl.Api.Infrastructure;
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
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();

app.MapGet("/", () => Results.Ok(new { name = "LTL SaaS API", status = "ok" }));
app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // 供本地调试与集成测试验证统一异常处理；仅开发环境暴露
    app.MapGet("/dev/throw", () =>
    {
        throw new InvalidOperationException("演示异常：验证统一 ProblemDetails 错误格式");
    });
}

app.Run();

// 供 WebApplicationFactory 集成测试作为入口类型
public partial class Program;
