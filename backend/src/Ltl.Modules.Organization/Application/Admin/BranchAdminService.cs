using System.Transactions;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Organization.Domain;
using Ltl.Modules.Organization.Infrastructure;
using Ltl.Modules.Platform.Application;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Organization.Application.Admin;

public sealed record BranchDto(Guid Id, string Code, string Name, string Status, DateTime CreatedAt);

public sealed record CreateBranchRequest(string Code, string Name);

public sealed record RenameRequest(string Name);

public interface IBranchAdminService
{
    Task<PageResult<BranchDto>> ListAsync(Guid operatorUserId, int page, int pageSize);
    Task<BranchDto> GetAsync(Guid operatorUserId, Guid branchId);
    Task<BranchDto> CreateAsync(Guid operatorUserId, CreateBranchRequest request);
    Task<BranchDto> RenameAsync(Guid operatorUserId, Guid branchId, string name);
    Task<BranchDto> DisableAsync(Guid operatorUserId, Guid branchId);
    Task<BranchDto> EnableAsync(Guid operatorUserId, Guid branchId);
}

/// <summary>
/// 网点管理（ORG-003）。权限：branch.read / branch.manage（实时校验）；
/// 写操作与审计同环境事务（TransactionScope）。
/// </summary>
public sealed class BranchAdminService(
    OrganizationDbContext db,
    IAuthorizationService authorization,
    IAuditLogService audit) : AdminServiceBase(db, authorization, audit), IBranchAdminService
{
    public async Task<PageResult<BranchDto>> ListAsync(Guid operatorUserId, int page, int pageSize)
    {
        await RequirePermissionAsync(operatorUserId, PermissionCodes.BranchRead, "organization.branch.read");
        var (normalizedPage, normalizedPageSize, skip) = NormalizePaging(page, pageSize);

        var total = await Db.Branches.CountAsync();
        var items = await Db.Branches
            .OrderBy(b => b.Code)
            .Skip(skip).Take(normalizedPageSize)
            .ToListAsync();

        return new PageResult<BranchDto>(
            items.Select(Map).ToList(), total, normalizedPage, normalizedPageSize);
    }

    public async Task<BranchDto> GetAsync(Guid operatorUserId, Guid branchId)
    {
        await RequirePermissionAsync(operatorUserId, PermissionCodes.BranchRead, "organization.branch.read");
        var branch = await Db.Branches.FindAsync([branchId])
            ?? throw new EntityNotFoundException("网点", branchId);
        return Map(branch);
    }

    public async Task<BranchDto> CreateAsync(Guid operatorUserId, CreateBranchRequest request)
    {
        await RequirePermissionAsync(operatorUserId, PermissionCodes.BranchManage, "organization.branch.create");
        var tenantId = RequireTenantId();

        if (await Db.Branches.AnyAsync(b => b.Code == request.Code.Trim()))
        {
            throw new ConflictException($"网点编码（{request.Code.Trim()}）已存在。");
        }

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var branch = Branch.Create(tenantId, request.Code, request.Name, DateTime.UtcNow, operatorUserId);
        Db.Branches.Add(branch);
        await Db.SaveChangesAsync();

        await RecordAuditAsync(
            "organization.branch.create", "branch", branch.Id, operatorUserId,
            new { code = branch.Code, name = branch.Name });
        scope.Complete();

        return Map(branch);
    }

    public async Task<BranchDto> RenameAsync(Guid operatorUserId, Guid branchId, string name)
    {
        await RequirePermissionAsync(operatorUserId, PermissionCodes.BranchManage, "organization.branch.rename");
        var branch = await RequireBranchAsync(branchId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        branch.Rename(name, DateTime.UtcNow, operatorUserId);
        await Db.SaveChangesAsync();

        await RecordAuditAsync(
            "organization.branch.rename", "branch", branch.Id, operatorUserId,
            new { code = branch.Code, name = branch.Name });
        scope.Complete();

        return Map(branch);
    }

    public async Task<BranchDto> DisableAsync(Guid operatorUserId, Guid branchId)
    {
        await RequirePermissionAsync(operatorUserId, PermissionCodes.BranchManage, "organization.branch.disable");
        var branch = await RequireBranchAsync(branchId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        branch.Disable(DateTime.UtcNow, operatorUserId);
        await Db.SaveChangesAsync();

        await RecordAuditAsync(
            "organization.branch.disable", "branch", branch.Id, operatorUserId, new { code = branch.Code });
        scope.Complete();

        return Map(branch);
    }

    public async Task<BranchDto> EnableAsync(Guid operatorUserId, Guid branchId)
    {
        await RequirePermissionAsync(operatorUserId, PermissionCodes.BranchManage, "organization.branch.enable");
        var branch = await RequireBranchAsync(branchId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        branch.Enable(DateTime.UtcNow, operatorUserId);
        await Db.SaveChangesAsync();

        await RecordAuditAsync(
            "organization.branch.enable", "branch", branch.Id, operatorUserId, new { code = branch.Code });
        scope.Complete();

        return Map(branch);
    }

    private async Task<Branch> RequireBranchAsync(Guid branchId) =>
        await Db.Branches.FindAsync([branchId])
        ?? throw new EntityNotFoundException("网点", branchId);

    private static BranchDto Map(Branch branch) => new(
        branch.Id, branch.Code, branch.Name, branch.Status.ToString(), branch.CreatedAt);
}
