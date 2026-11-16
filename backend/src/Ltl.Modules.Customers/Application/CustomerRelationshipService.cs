using Ltl.Modules.Customers.Domain;
using Ltl.Modules.Customers.Infrastructure;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Customers.Application;

/// <summary>
/// 客户-网点服务关系实现。关联表与主体表无导航关系（外键仅存在于数据库层），
/// 先确认主体存在，再单独保存关联（EF 不保证跨实体插入顺序，ORG-001 经验）。
/// </summary>
public sealed class CustomerRelationshipService(CustomersDbContext db) : ICustomerRelationshipService
{
    public async Task GrantBranchServiceAsync(Guid customerId, Guid branchId, Guid? operatedBy = null)
    {
        var customer = await RequireCustomerAsync(customerId);
        await RequireBranchReachableAsync(branchId);

        var exists = await db.BranchRelations.AnyAsync(x =>
            x.TenantId == customer.TenantId && x.CustomerId == customerId && x.BranchId == branchId);
        if (exists)
        {
            return;
        }

        db.BranchRelations.Add(CustomerBranchRelation.Create(
            customer.TenantId, customerId, branchId, DateTime.UtcNow, operatedBy));
        await db.SaveChangesAsync();
    }

    public async Task RevokeBranchServiceAsync(Guid customerId, Guid branchId)
    {
        var link = await db.BranchRelations.SingleOrDefaultAsync(x =>
            x.CustomerId == customerId && x.BranchId == branchId);
        if (link is null)
        {
            return;
        }

        db.BranchRelations.Remove(link);
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyCollection<Guid>> GetServicedBranchIdsAsync(Guid customerId)
    {
        _ = await RequireCustomerAsync(customerId);

        return await db.BranchRelations
            .Where(x => x.CustomerId == customerId)
            .Select(x => x.BranchId)
            .ToListAsync();
    }

    public async Task<IReadOnlyCollection<Guid>> GetCustomersServedByBranchAsync(Guid branchId)
    {
        await RequireBranchReachableAsync(branchId);

        return await db.BranchRelations
            .Where(x => x.BranchId == branchId)
            .Select(x => x.CustomerId)
            .ToListAsync();
    }

    private async Task<Customer> RequireCustomerAsync(Guid customerId)
    {
        return await db.Customers.SingleOrDefaultAsync(c => c.Id == customerId)
            ?? throw new EntityNotFoundException("客户", customerId);
    }

    /// <summary>网点属 Organization 模块数据，本模块无法加载其实体；用原生 SQL 验证当前租户内可达（显式租户条件，AGENTS.md §5.1）。</summary>
    private async Task RequireBranchReachableAsync(Guid branchId)
    {
        var currentTenantId = db.CurrentTenantId;
        var reachable = await db.Database
            .SqlQuery<bool>($"""
                SELECT EXISTS (
                    SELECT 1 FROM branches
                    WHERE id = {branchId}
                      AND tenant_id = {currentTenantId}) AS "Value"
                """)
            .SingleAsync();

        if (!reachable)
        {
            throw new EntityNotFoundException("网点", branchId);
        }
    }
}
