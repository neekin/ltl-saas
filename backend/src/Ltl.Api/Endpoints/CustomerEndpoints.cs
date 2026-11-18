using System.Security.Claims;
using Ltl.Modules.Customers.Application;
using Ltl.Modules.Organization.Application.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ltl.Api.Endpoints;

/// <summary>客户管理端点（CUS-002）。</summary>
public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this WebApplication app)
    {
        var customers = app.MapGroup("/api/customers");
        customers.MapGet("/", async (int? page, int? pageSize, string? keyword, ClaimsPrincipal user, ICustomerAdminService service) =>
            Results.Ok(await service.ListAsync(RequireUserId(user), page ?? 1, pageSize ?? 20, keyword)));
        customers.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, ICustomerAdminService service) =>
            Results.Ok(await service.GetAsync(RequireUserId(user), id)));
        customers.MapPost("/", async (CreateCustomerRequest request, ClaimsPrincipal user, ICustomerAdminService service) =>
            Results.Ok(await service.CreateAsync(RequireUserId(user), request)));
        customers.MapPut("/{id:guid}", async (Guid id, UpdateCustomerRequest request, ClaimsPrincipal user, ICustomerAdminService service) =>
            Results.Ok(await service.UpdateAsync(RequireUserId(user), id, request)));
        customers.MapPut("/{id:guid}/credit-policy", async (Guid id, SetCreditPolicyRequest request, ClaimsPrincipal user, ICustomerAdminService service) =>
            Results.Ok(await service.SetCreditPolicyAsync(RequireUserId(user), id, request)));
        customers.MapPost("/{id:guid}/disable", async (Guid id, ClaimsPrincipal user, ICustomerAdminService service) =>
            Results.Ok(await service.DisableAsync(RequireUserId(user), id)));
        customers.MapPost("/{id:guid}/enable", async (Guid id, ClaimsPrincipal user, ICustomerAdminService service) =>
            Results.Ok(await service.EnableAsync(RequireUserId(user), id)));
        customers.MapPost("/{id:guid}/contacts", async (Guid id, AddContactRequest request, ClaimsPrincipal user, ICustomerAdminService service) =>
            Results.Ok(await service.AddContactAsync(RequireUserId(user), id, request)));
        customers.MapDelete("/{id:guid}/contacts/{contactId:guid}", async (
            Guid id, Guid contactId, ClaimsPrincipal user, ICustomerAdminService service) =>
        {
            var result = await service.RemoveContactAsync(RequireUserId(user), id, contactId);
            return Results.Ok(result);
        });
        customers.MapPost("/{id:guid}/addresses", async (Guid id, AddAddressRequest request, ClaimsPrincipal user, ICustomerAdminService service) =>
            Results.Ok(await service.AddAddressAsync(RequireUserId(user), id, request)));
        customers.MapDelete("/{id:guid}/addresses/{addressId:guid}", async (
            Guid id, Guid addressId, ClaimsPrincipal user, ICustomerAdminService service) =>
            Results.Ok(await service.RemoveAddressAsync(RequireUserId(user), id, addressId)));
        customers.MapPost("/{id:guid}/branches", async (Guid id, GrantBranchRequest request, ClaimsPrincipal user, ICustomerAdminService service) =>
            Results.Ok(await service.GrantBranchAsync(RequireUserId(user), id, request.BranchId)));
        customers.MapDelete("/{id:guid}/branches/{branchId:guid}", async (
            Guid id, Guid branchId, ClaimsPrincipal user, ICustomerAdminService service) =>
            Results.Ok(await service.RevokeBranchAsync(RequireUserId(user), id, branchId)));
    }

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new AuthenticationFailedException("无效的认证凭据");
}
