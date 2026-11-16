var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { name = "LTL SaaS API", status = "ok" }));
app.MapHealthChecks("/health");

app.Run();
