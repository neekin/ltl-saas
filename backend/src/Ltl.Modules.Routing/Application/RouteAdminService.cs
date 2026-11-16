using System.Transactions;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Platform.Application;
using Ltl.Modules.Routing.Domain;
using Ltl.Modules.Routing.Infrastructure;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Routing.Application;

public sealed record RouteDto(
    Guid Id, string Code, string Name, Guid OriginBranchId, Guid DestBranchId, string Status, DateTime CreatedAt);

public sealed record RouteStopDto(Guid Id, Guid BranchId, int Sequence, string? Remark);

public sealed record RouteStopSpecDto(Guid BranchId, int Sequence, string? Remark);

public sealed record RouteDetailDto(
    Guid Id, string Code, string Name, Guid OriginBranchId, Guid DestBranchId, string Status,
    IReadOnlyList<RouteStopDto> Stops);

public sealed record TransportPlanDto(
    Guid Id, Guid RouteId, string Name, string Status,
    IReadOnlyList<PlanSegmentDto> Segments);

public sealed record PlanSegmentDto(Guid Id, int Sequence, Guid FromBranchId, Guid ToBranchId);

public sealed record CreatePlanRequest(string Name, IReadOnlyList<PlanSegmentSpecDto> Segments, string? Note);

public sealed record PlanSegmentSpecDto(Guid FromBranchId, Guid ToBranchId, int Sequence);

/// <summary>
/// 线路管理服务（ROUTE-002）：线路 CRUD、站点编辑（RouteSpec 校验）、运输方案（TransportPlanSpec 校验）。
/// 权限：route.read / route.manage；写操作 + 审计同环境事务。
/// </summary>
public interface IRouteAdminService
{
    Task<PageResultRoutes> ListRoutesAsync(Guid operatorUserId, int page, int pageSize, string? keyword);
    Task<RouteDetailDto> GetRouteAsync(Guid operatorUserId, Guid routeId);
    Task<RouteDetailDto> CreateRouteAsync(Guid operatorUserId, CreateRouteRequest request);
    Task<RouteDetailDto> RenameRouteAsync(Guid operatorUserId, Guid routeId, string name);
    Task<RouteDetailDto> DisableRouteAsync(Guid operatorUserId, Guid routeId);
    Task<RouteDetailDto> EnableRouteAsync(Guid operatorUserId, Guid routeId);
    Task<RouteDetailDto> ReplaceStopsAsync(Guid operatorUserId, Guid routeId, IReadOnlyList<RouteStopSpecDto> stops);
    Task<IReadOnlyList<TransportPlanDto>> ListPlansAsync(Guid operatorUserId, Guid routeId);
    Task<TransportPlanDto> CreatePlanAsync(Guid operatorUserId, Guid routeId, CreatePlanRequest request);
}

public sealed record PageResultRoutes(IReadOnlyList<RouteDto> Items, int Total, int Page, int PageSize);

public sealed record CreateRouteRequest(string Code, string Name, Guid OriginBranchId, Guid DestBranchId);

public sealed class RouteAdminService(
    RoutingDbContext db,
    IAuthorizationService authorization,
    IAuditLogService audit) : IRouteAdminService
{
    public async Task<PageResultRoutes> ListRoutesAsync(
        Guid operatorUserId, int page, int pageSize, string? keyword)
    {
        await RequirePermissionAsync(operatorUserId, RoutePermissionCodes.RouteRead, RoutePermissionCodes.RouteRead);
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = db.Routes.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var trimmed = keyword.Trim();
            query = query.Where(r => r.Code.Contains(trimmed) || r.Name.Contains(trimmed));
        }

        var total = await query.CountAsync();
        var items = await query.OrderBy(r => r.Code).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PageResultRoutes(
            items.Select(r => new RouteDto(
                r.Id, r.Code, r.Name, r.OriginBranchId, r.DestinationBranchId, r.Status.ToString(), r.CreatedAt)).ToList(),
            total, page, pageSize);
    }

    public async Task<RouteDetailDto> GetRouteAsync(Guid operatorUserId, Guid routeId)
    {
        await RequirePermissionAsync(operatorUserId, RoutePermissionCodes.RouteRead, RoutePermissionCodes.RouteRead);
        return await MapDetailAsync(await RequireRouteAsync(routeId));
    }

    public async Task<RouteDetailDto> CreateRouteAsync(Guid operatorUserId, CreateRouteRequest request)
    {
        await RequirePermissionAsync(operatorUserId, RoutePermissionCodes.RouteManage, "route.create");
        var tenantId = RequireTenantId();

        if (await db.Routes.AnyAsync(r => r.Code == request.Code.Trim()))
        {
            throw new ConflictException($"线路编码（{request.Code.Trim()}）已存在。");
        }

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var route = Route.Create(
            tenantId, request.Code, request.Name, request.OriginBranchId, request.DestBranchId,
            DateTime.UtcNow, operatorUserId);
        db.Routes.Add(route);
        await db.SaveChangesAsync();

        await audit.RecordAsync(
            "route.create", "route", route.Id, operatorUserId,
            new { code = route.Code, name = route.Name });
        scope.Complete();

        return await MapDetailAsync(route);
    }

    public async Task<RouteDetailDto> RenameRouteAsync(Guid operatorUserId, Guid routeId, string name)
    {
        await RequirePermissionAsync(operatorUserId, RoutePermissionCodes.RouteManage, "route.rename");
        var route = await RequireRouteAsync(routeId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        route.Rename(name, DateTime.UtcNow, operatorUserId);
        await db.SaveChangesAsync();
        await audit.RecordAsync("route.rename", "route", route.Id, operatorUserId, new { code = route.Code, name });
        scope.Complete();

        return await MapDetailAsync(route);
    }

    public async Task<RouteDetailDto> DisableRouteAsync(Guid operatorUserId, Guid routeId)
    {
        await RequirePermissionAsync(operatorUserId, RoutePermissionCodes.RouteManage, "route.disable");
        var route = await RequireRouteAsync(routeId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        route.Disable(DateTime.UtcNow, operatorUserId);
        await db.SaveChangesAsync();
        await audit.RecordAsync("route.disable", "route", route.Id, operatorUserId, new { code = route.Code });
        scope.Complete();

        return await MapDetailAsync(route);
    }

    public async Task<RouteDetailDto> EnableRouteAsync(Guid operatorUserId, Guid routeId)
    {
        await RequirePermissionAsync(operatorUserId, RoutePermissionCodes.RouteManage, "route.enable");
        var route = await RequireRouteAsync(routeId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        route.Enable(DateTime.UtcNow, operatorUserId);
        await db.SaveChangesAsync();
        await audit.RecordAsync("route.enable", "route", route.Id, operatorUserId, new { code = route.Code });
        scope.Complete();

        return await MapDetailAsync(route);
    }

    /// <summary>站点整体替换（RouteSpec 校验：连续 0..N-1、首尾=起终点；历史运单不受影响）。</summary>
    public async Task<RouteDetailDto> ReplaceStopsAsync(
        Guid operatorUserId, Guid routeId, IReadOnlyList<RouteStopSpecDto> stops)
    {
        await RequirePermissionAsync(operatorUserId, RoutePermissionCodes.RouteManage, "route.replace_stops");
        var tenantId = RequireTenantId();
        var route = await RequireRouteAsync(routeId);

        var stopSpecs = stops.Select(s => new RouteSpec.StopSpec(s.BranchId, s.Sequence)).ToList();
        var errors = RouteSpec.Validate(route.OriginBranchId, route.DestinationBranchId, stopSpecs);
        if (errors.Count > 0)
        {
            throw new ArgumentException($"站点校验未通过：{string.Join("；", errors)}", nameof(stops));
        }

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var oldStops = await db.Stops.Where(s => s.RouteId == routeId).ToListAsync();
        foreach (var old in oldStops)
        {
            db.Stops.Remove(old);
        }

        foreach (var spec in stops.OrderBy(s => s.Sequence))
        {
            db.Stops.Add(RouteStop.Create(
                tenantId, routeId, spec.BranchId, spec.Sequence, DateTime.UtcNow, spec.Remark, operatorUserId));
        }

        await db.SaveChangesAsync();
        await audit.RecordAsync(
            "route.replace_stops", "route", route.Id, operatorUserId,
            new { code = route.Code, stopCount = stops.Count });
        scope.Complete();

        return await MapDetailAsync(route);
    }

    public async Task<IReadOnlyList<TransportPlanDto>> ListPlansAsync(Guid operatorUserId, Guid routeId)
    {
        await RequirePermissionAsync(operatorUserId, RoutePermissionCodes.RouteRead, RoutePermissionCodes.RouteRead);
        await RequireRouteAsync(routeId);

        var plans = await db.Plans.Where(p => p.RouteId == routeId).OrderBy(p => p.CreatedAt).ToListAsync();
        var planIds = plans.Select(p => p.Id).ToList();
        var segments = await db.PlanSegments
            .Where(s => planIds.Contains(s.PlanId))
            .OrderBy(s => s.Sequence)
            .ToListAsync();

        return plans.Select(p => new TransportPlanDto(
            p.Id, p.RouteId, p.Name, p.Status.ToString(),
            segments.Where(s => s.PlanId == p.Id).Select(s => new PlanSegmentDto(
                s.Id, s.Sequence, s.FromBranchId, s.ToBranchId)).ToList())).ToList();
    }

    /// <summary>创建运输方案（区段经 TransportPlanSpec 校验：连续、首尾相连、落在线路站点内）。</summary>
    public async Task<TransportPlanDto> CreatePlanAsync(
        Guid operatorUserId, Guid routeId, CreatePlanRequest request)
    {
        await RequirePermissionAsync(operatorUserId, RoutePermissionCodes.RouteManage, "plan.create");
        var tenantId = RequireTenantId();
        var route = await RequireRouteAsync(routeId);

        var routeStops = await db.Stops
            .Where(s => s.RouteId == routeId)
            .OrderBy(s => s.Sequence)
            .Select(s => s.BranchId)
            .ToListAsync();
        var segmentSpecs = request.Segments
            .Select(s => new TransportPlanSpec.SegmentSpec(s.FromBranchId, s.ToBranchId, s.Sequence))
            .ToList();
        var errors = TransportPlanSpec.Validate(
            route.OriginBranchId, route.DestinationBranchId, routeStops, segmentSpecs);
        if (errors.Count > 0)
        {
            throw new ArgumentException($"运输方案校验未通过：{string.Join("；", errors)}", nameof(request));
        }

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var plan = TransportPlan.Create(tenantId, routeId, request.Name, DateTime.UtcNow, operatorUserId);
        db.Plans.Add(plan);
        await db.SaveChangesAsync();

        foreach (var segment in request.Segments.OrderBy(s => s.Sequence))
        {
            db.PlanSegments.Add(TransportPlanSegment.Create(
                tenantId, plan.Id, segment.Sequence, segment.FromBranchId, segment.ToBranchId,
                DateTime.UtcNow, operatorUserId));
        }

        await db.SaveChangesAsync();
        await audit.RecordAsync(
            "route.plan.create", "transport_plan", plan.Id, operatorUserId,
            new { routeCode = route.Code, name = plan.Name, segmentCount = request.Segments.Count });
        scope.Complete();

        var segments = await db.PlanSegments
            .Where(s => s.PlanId == plan.Id)
            .OrderBy(s => s.Sequence)
            .ToListAsync();
        return new TransportPlanDto(
            plan.Id, plan.RouteId, plan.Name, plan.Status.ToString(),
            segments.Select(s => new PlanSegmentDto(s.Id, s.Sequence, s.FromBranchId, s.ToBranchId)).ToList());
    }

    // ---------- 辅助 ----------

    private async Task RequirePermissionAsync(Guid operatorUserId, string permissionCode, string action)
    {
        if (!await authorization.HasPermissionAsync(operatorUserId, permissionCode))
        {
            throw new PermissionDeniedException(action);
        }
    }

    private Guid RequireTenantId() =>
        db.CurrentTenantId ?? throw new TenantContextMissingException();

    private async Task<Route> RequireRouteAsync(Guid routeId) =>
        await db.Routes.FindAsync([routeId]) ?? throw new EntityNotFoundException("线路", routeId);

    private async Task<RouteDetailDto> MapDetailAsync(Route route)
    {
        var stops = await db.Stops
            .Where(s => s.RouteId == route.Id)
            .OrderBy(s => s.Sequence)
            .ToListAsync();
        return new RouteDetailDto(
            route.Id, route.Code, route.Name, route.OriginBranchId, route.DestinationBranchId,
            route.Status.ToString(),
            stops.Select(s => new RouteStopDto(s.Id, s.BranchId, s.Sequence, s.Remark)).ToList());
    }
}
