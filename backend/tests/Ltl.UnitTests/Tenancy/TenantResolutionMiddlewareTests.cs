using System.Security.Claims;
using Ltl.Modules.Tenancy.Application;
using Ltl.Modules.Tenancy.Infrastructure;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Ltl.UnitTests.Tenancy;

public class TenantResolutionMiddlewareTests
{
    private static (DefaultHttpContext Context, TenantContext TenantContext) Setup(ClaimsPrincipal? user)
    {
        var context = new DefaultHttpContext { User = user ?? new ClaimsPrincipal() };
        context.Response.Body = new MemoryStream();
        return (context, new TenantContext());
    }

    private static ClaimsPrincipal AuthenticatedWithClaim(string claimValue)
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(TenantClaimTypes.TenantId, claimValue));
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task Authenticated_WithValidClaim_ShouldSetTenantContext()
    {
        var (context, tenantContext) = Setup(AuthenticatedWithClaim(Guid.NewGuid().ToString()));
        var pipelineCalled = false;
        RequestDelegate next = (_) =>
        {
            pipelineCalled = true;
            return Task.CompletedTask;
        };
        var middleware = new TenantResolutionMiddleware(next);

        await middleware.InvokeAsync(context, tenantContext);

        Assert.NotNull(tenantContext.TenantId);
        Assert.True(pipelineCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_WithInvalidClaim_ShouldReturn401AndStopPipeline()
    {
        var (context, tenantContext) = Setup(AuthenticatedWithClaim("not-a-guid"));
        var pipelineCalled = false;
        RequestDelegate next = (_) =>
        {
            pipelineCalled = true;
            return Task.CompletedTask;
        };
        var middleware = new TenantResolutionMiddleware(next);

        await middleware.InvokeAsync(context, tenantContext);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Null(tenantContext.TenantId);
        Assert.False(pipelineCalled);
    }

    [Fact]
    public async Task Authenticated_WithEmptyGuidClaim_ShouldReturn401()
    {
        var (context, tenantContext) = Setup(AuthenticatedWithClaim(Guid.Empty.ToString()));
        RequestDelegate next = (_) => Task.CompletedTask;
        var middleware = new TenantResolutionMiddleware(next);

        await middleware.InvokeAsync(context, tenantContext);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Null(tenantContext.TenantId);
    }

    [Fact]
    public async Task Anonymous_ShouldContinueWithoutTenantContext()
    {
        var (context, tenantContext) = Setup(null);
        var pipelineCalled = false;
        RequestDelegate next = (_) =>
        {
            pipelineCalled = true;
            return Task.CompletedTask;
        };
        var middleware = new TenantResolutionMiddleware(next);

        await middleware.InvokeAsync(context, tenantContext);

        Assert.Null(tenantContext.TenantId);
        Assert.True(pipelineCalled);
    }
}
