using System.Transactions;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Platform.Application;
using Ltl.Modules.Customers.Domain;
using Ltl.Modules.Customers.Infrastructure;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Customers.Application;

public sealed record CustomerDto(
    Guid Id, string Code, string Name, string Status, string? Remark, DateTime CreatedAt,
    decimal? CreditLimit, int? PaymentTermsDays);

public sealed record CustomerContactDto(
    Guid Id, string Name, string Phone, bool IsPrimary, string? Remark);

public sealed record CustomerAddressDto(
    Guid Id, string Address, string? Label, bool IsDefault, string? Remark);

public sealed record CustomerDetailDto(
    Guid Id, string Code, string Name, string Status, string? Remark, DateTime CreatedAt,
    decimal? CreditLimit, int? PaymentTermsDays,
    IReadOnlyList<CustomerContactDto> Contacts,
    IReadOnlyList<CustomerAddressDto> Addresses,
    IReadOnlyList<Guid> ServicedBranchIds);

public sealed record CreateCustomerRequest(string Code, string Name, string? Remark);

/// <summary>月结信用政策：额度（元）与账期（天）；传 null 表示清除设置。</summary>
public sealed record SetCreditPolicyRequest(decimal? CreditLimit, int? PaymentTermsDays);

public sealed record UpdateCustomerRequest(string Name, string? Remark);

public sealed record AddContactRequest(string Name, string Phone, bool IsPrimary, string? Remark);

public sealed record AddAddressRequest(string Address, string? Label, bool IsDefault, string? Remark);

public sealed record GrantBranchRequest(Guid BranchId);

/// <summary>
/// 客户管理服务（CUS-002）：客户 CRUD + 联系人/地址管理 + 客户-网点授权（复用 CUS-001 关系服务）。
/// 权限：customer.read / customer.manage（实时校验）；写操作 + 审计同环境事务（ORG-003 样板）。
/// </summary>
public interface ICustomerAdminService
{
    Task<PageResultCustomers> ListAsync(Guid operatorUserId, int page, int pageSize, string? keyword);
    Task<CustomerDetailDto> GetAsync(Guid operatorUserId, Guid customerId);
    Task<CustomerDto> CreateAsync(Guid operatorUserId, CreateCustomerRequest request);
    Task<CustomerDto> UpdateAsync(Guid operatorUserId, Guid customerId, UpdateCustomerRequest request);
    Task<CustomerDto> SetCreditPolicyAsync(Guid operatorUserId, Guid customerId, SetCreditPolicyRequest request);
    Task<CustomerDto> DisableAsync(Guid operatorUserId, Guid customerId);
    Task<CustomerDto> EnableAsync(Guid operatorUserId, Guid customerId);
    Task<CustomerDetailDto> AddContactAsync(Guid operatorUserId, Guid customerId, AddContactRequest request);
    Task<CustomerDetailDto> RemoveContactAsync(Guid operatorUserId, Guid customerId, Guid contactId);
    Task<CustomerDetailDto> AddAddressAsync(Guid operatorUserId, Guid customerId, AddAddressRequest request);
    Task<CustomerDetailDto> RemoveAddressAsync(Guid operatorUserId, Guid customerId, Guid addressId);
    Task<CustomerDetailDto> GrantBranchAsync(Guid operatorUserId, Guid customerId, Guid branchId);
    Task<CustomerDetailDto> RevokeBranchAsync(Guid operatorUserId, Guid customerId, Guid branchId);
}

public sealed record PageResultCustomers(
    IReadOnlyList<CustomerDto> Items, int Total, int Page, int PageSize);

public sealed class CustomerAdminService(
    CustomersDbContext db,
    IAuthorizationService authorization,
    IAuditLogService audit,
    ICustomerRelationshipService relationshipService) : ICustomerAdminService
{
    public async Task<PageResultCustomers> ListAsync(
        Guid operatorUserId, int page, int pageSize, string? keyword)
    {
        await RequirePermissionAsync(operatorUserId, CustomerPermissionCodes.CustomerRead, CustomerPermissionCodes.CustomerRead);
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = db.Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var trimmed = keyword.Trim();
            query = query.Where(c => c.Code.Contains(trimmed) || c.Name.Contains(trimmed));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(c => c.Code)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return new PageResultCustomers(
            items.Select(Map).ToList(), total, page, pageSize);
    }

    public async Task<CustomerDetailDto> GetAsync(Guid operatorUserId, Guid customerId)
    {
        await RequirePermissionAsync(operatorUserId, CustomerPermissionCodes.CustomerRead, CustomerPermissionCodes.CustomerRead);
        var customer = await RequireCustomerAsync(customerId);
        return await MapDetailAsync(customer);
    }

    public async Task<CustomerDto> CreateAsync(Guid operatorUserId, CreateCustomerRequest request)
    {
        await RequirePermissionAsync(operatorUserId, CustomerPermissionCodes.CustomerManage, "customer.create");
        var tenantId = RequireTenantId();

        if (await db.Customers.AnyAsync(c => c.Code == request.Code.Trim()))
        {
            throw new ConflictException($"客户编码（{request.Code.Trim()}）已存在。");
        }

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var customer = Customer.Create(
            tenantId, request.Code, request.Name, DateTime.UtcNow, request.Remark, operatorUserId);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        await audit.RecordAsync(
            "customer.create", "customer", customer.Id, operatorUserId,
            new { code = customer.Code, name = customer.Name });
        scope.Complete();

        return Map(customer);
    }

    public async Task<CustomerDto> UpdateAsync(Guid operatorUserId, Guid customerId, UpdateCustomerRequest request)
    {
        await RequirePermissionAsync(operatorUserId, CustomerPermissionCodes.CustomerManage, "customer.update");
        var customer = await RequireCustomerAsync(customerId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        customer.UpdateProfile(request.Name, request.Remark, DateTime.UtcNow, operatorUserId);
        await db.SaveChangesAsync();
        await audit.RecordAsync(
            "customer.update", "customer", customer.Id, operatorUserId,
            new { code = customer.Code, name = customer.Name });
        scope.Complete();

        return Map(customer);
    }

    /// <summary>
    /// 设置月结信用政策（额度/账期）。金额精度由数据库 NUMERIC(18,2) 约束。
    /// 开单前的信用额度检查（WaybillService.CheckCreditLimitAsync）依赖此处维护的额度。
    /// </summary>
    public async Task<CustomerDto> SetCreditPolicyAsync(
        Guid operatorUserId, Guid customerId, SetCreditPolicyRequest request)
    {
        await RequirePermissionAsync(operatorUserId, CustomerPermissionCodes.CustomerManage, "customer.credit-policy");
        var customer = await RequireCustomerAsync(customerId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        customer.SetCreditPolicy(request.CreditLimit, request.PaymentTermsDays, DateTime.UtcNow, operatorUserId);
        await db.SaveChangesAsync();
        await audit.RecordAsync(
            "customer.credit-policy", "customer", customer.Id, operatorUserId,
            new { code = customer.Code, creditLimit = request.CreditLimit, paymentTermsDays = request.PaymentTermsDays });
        scope.Complete();

        return Map(customer);
    }

    public async Task<CustomerDto> DisableAsync(Guid operatorUserId, Guid customerId)
    {
        await RequirePermissionAsync(operatorUserId, CustomerPermissionCodes.CustomerManage, "customer.disable");
        var customer = await RequireCustomerAsync(customerId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        customer.Disable(DateTime.UtcNow, operatorUserId);
        await db.SaveChangesAsync();
        await audit.RecordAsync(
            "customer.disable", "customer", customer.Id, operatorUserId, new { code = customer.Code });
        scope.Complete();

        return Map(customer);
    }

    public async Task<CustomerDto> EnableAsync(Guid operatorUserId, Guid customerId)
    {
        await RequirePermissionAsync(operatorUserId, CustomerPermissionCodes.CustomerManage, "customer.enable");
        var customer = await RequireCustomerAsync(customerId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        customer.Enable(DateTime.UtcNow, operatorUserId);
        await db.SaveChangesAsync();
        await audit.RecordAsync(
            "customer.enable", "customer", customer.Id, operatorUserId, new { code = customer.Code });
        scope.Complete();

        return Map(customer);
    }

    public async Task<CustomerDetailDto> AddContactAsync(
        Guid operatorUserId, Guid customerId, AddContactRequest request)
    {
        await RequirePermissionAsync(operatorUserId, CustomerPermissionCodes.CustomerManage, "contact.add");
        var customer = await RequireCustomerAsync(customerId);

        var tenantId = RequireTenantId();

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        // 主联系人唯一（部分唯一索引）；新主联系人 → 清除旧主标记
        if (request.IsPrimary)
        {
            await db.Contacts
                .Where(c => c.CustomerId == customerId && c.IsPrimary)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsPrimary, false));
        }

        db.Contacts.Add(CustomerContact.Create(
            tenantId, customerId, request.Name, request.Phone,
            DateTime.UtcNow, request.IsPrimary, request.Remark, operatorUserId));
        await db.SaveChangesAsync();

        await audit.RecordAsync(
            "customer.contact.add", "customer", customer.Id, operatorUserId,
            new { code = customer.Code, contact = request.Name });
        var detail = await MapDetailAsync(customer);
        scope.Complete();

        return detail;
    }

    public async Task<CustomerDetailDto> RemoveContactAsync(
        Guid operatorUserId, Guid customerId, Guid contactId)
    {
        await RequirePermissionAsync(operatorUserId, CustomerPermissionCodes.CustomerManage, "contact.remove");
        var customer = await RequireCustomerAsync(customerId);

        var contact = await db.Contacts.FindAsync([contactId])
            ?? throw new EntityNotFoundException("联系人", contactId);
        if (contact.CustomerId != customerId)
        {
            throw new EntityNotFoundException("联系人", contactId);
        }

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        db.Contacts.Remove(contact);
        await db.SaveChangesAsync();
        await audit.RecordAsync(
            "customer.contact.remove", "customer", customer.Id, operatorUserId,
            new { code = customer.Code, contact = contact.Name });
        var detail = await MapDetailAsync(customer);
        scope.Complete();

        return detail;
    }

    public async Task<CustomerDetailDto> AddAddressAsync(
        Guid operatorUserId, Guid customerId, AddAddressRequest request)
    {
        await RequirePermissionAsync(operatorUserId, CustomerPermissionCodes.CustomerManage, "address.add");
        var customer = await RequireCustomerAsync(customerId);

        var tenantId = RequireTenantId();

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        if (request.IsDefault)
        {
            await db.Addresses
                .Where(a => a.CustomerId == customerId && a.IsDefault)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false));
        }

        db.Addresses.Add(CustomerAddress.Create(
            tenantId, customerId, request.Address, DateTime.UtcNow,
            request.Label, request.IsDefault, request.Remark, operatorUserId));
        await db.SaveChangesAsync();

        await audit.RecordAsync(
            "customer.address.add", "customer", customer.Id, operatorUserId,
            new { code = customer.Code, address = request.Address });
        var detail = await MapDetailAsync(customer);
        scope.Complete();

        return detail;
    }

    public async Task<CustomerDetailDto> RemoveAddressAsync(
        Guid operatorUserId, Guid customerId, Guid addressId)
    {
        await RequirePermissionAsync(operatorUserId, CustomerPermissionCodes.CustomerManage, "address.remove");
        var customer = await RequireCustomerAsync(customerId);

        var address = await db.Addresses.FindAsync([addressId])
            ?? throw new EntityNotFoundException("地址", addressId);
        if (address.CustomerId != customerId)
        {
            throw new EntityNotFoundException("地址", addressId);
        }

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        db.Addresses.Remove(address);
        await db.SaveChangesAsync();
        await audit.RecordAsync(
            "customer.address.remove", "customer", customer.Id, operatorUserId,
            new { code = customer.Code, address = address.Address });
        var detail = await MapDetailAsync(customer);
        scope.Complete();

        return detail;
    }

    public async Task<CustomerDetailDto> GrantBranchAsync(
        Guid operatorUserId, Guid customerId, Guid branchId)
    {
        await RequirePermissionAsync(operatorUserId, CustomerPermissionCodes.CustomerManage, "branch.grant");
        var customer = await RequireCustomerAsync(customerId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        await relationshipService.GrantBranchServiceAsync(customerId, branchId, operatorUserId);
        await audit.RecordAsync(
            "customer.branch.grant", "customer", customer.Id, operatorUserId,
            new { code = customer.Code, branchId });
        var detail = await MapDetailAsync(customer);
        scope.Complete();

        return detail;
    }

    public async Task<CustomerDetailDto> RevokeBranchAsync(
        Guid operatorUserId, Guid customerId, Guid branchId)
    {
        await RequirePermissionAsync(operatorUserId, CustomerPermissionCodes.CustomerManage, "branch.revoke");
        var customer = await RequireCustomerAsync(customerId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        await relationshipService.RevokeBranchServiceAsync(customerId, branchId);
        await audit.RecordAsync(
            "customer.branch.revoke", "customer", customer.Id, operatorUserId,
            new { code = customer.Code, branchId });
        var detail = await MapDetailAsync(customer);
        scope.Complete();

        return detail;
    }

    // ---------- 辅助 ----------

    private Guid RequireTenantId() =>
        db.CurrentTenantId ?? throw new TenantContextMissingException();

    private async Task RequirePermissionAsync(Guid operatorUserId, string permissionCode, string action)
    {
        if (!await authorization.HasPermissionAsync(operatorUserId, permissionCode))
        {
            throw new PermissionDeniedException(action);
        }
    }

    private async Task<Customer> RequireCustomerAsync(Guid customerId) =>
        await db.Customers.FindAsync([customerId])
        ?? throw new EntityNotFoundException("客户", customerId);

    private async Task<CustomerDetailDto> MapDetailAsync(Customer customer)
    {
        var contacts = await db.Contacts
            .Where(c => c.CustomerId == customer.Id)
            .OrderByDescending(c => c.IsPrimary).ThenBy(c => c.Name)
            .ToListAsync();
        var addresses = await db.Addresses
            .Where(a => a.CustomerId == customer.Id)
            .OrderByDescending(a => a.IsDefault).ThenBy(a => a.Address)
            .ToListAsync();
        var branchIds = await relationshipService.GetServicedBranchIdsAsync(customer.Id);

        return new CustomerDetailDto(
            customer.Id, customer.Code, customer.Name, customer.Status.ToString(),
            customer.Remark, customer.CreatedAt,
            customer.CreditLimit, customer.PaymentTermsDays,
            contacts.Select(c => new CustomerContactDto(
                c.Id, c.Name, c.Phone, c.IsPrimary, c.Remark)).ToList(),
            addresses.Select(a => new CustomerAddressDto(
                a.Id, a.Address, a.Label, a.IsDefault, a.Remark)).ToList(),
            branchIds.ToList());
    }

    private static CustomerDto Map(Customer customer) => new(
        customer.Id, customer.Code, customer.Name, customer.Status.ToString(),
        customer.Remark, customer.CreatedAt,
        customer.CreditLimit, customer.PaymentTermsDays);
}
