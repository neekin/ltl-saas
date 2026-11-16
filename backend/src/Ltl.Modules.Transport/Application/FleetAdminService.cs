using System.Transactions;
using Ltl.Modules.Organization.Application.Admin;
using Ltl.Modules.Organization.Application.Authorization;
using Ltl.Modules.Platform.Application;
using Ltl.Modules.Transport.Domain;
using Ltl.Modules.Transport.Infrastructure;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Transport.Application;

public sealed record VehicleDto(
    Guid Id, string PlateNo, string VehicleType, decimal MaxLoadKg, decimal MaxVolumeM3,
    string Status, string? Note);

public sealed record DriverDto(Guid Id, string Name, string Phone, string LicenseNo, string Status, string? Note);

public sealed record CreateVehicleRequest(string PlateNo, string VehicleType, decimal MaxLoadKg, decimal MaxVolumeM3, string? Note);

public sealed record UpdateVehicleRequest(string VehicleType, decimal MaxLoadKg, decimal MaxVolumeM3, string? Note);

public sealed record CreateDriverRequest(string Name, string Phone, string LicenseNo, string? Note);

public sealed record UpdateDriverRequest(string Name, string Phone, string? Note);

/// <summary>车辆/司机档案管理（VEHICLE-001）。权限 transport.vehicle.read/manage；写操作 + 审计同环境事务。</summary>
public interface IFleetAdminService
{
    Task<PageResult<VehicleDto>> ListVehiclesAsync(Guid operatorUserId, int page, int pageSize);
    Task<VehicleDto> CreateVehicleAsync(Guid operatorUserId, CreateVehicleRequest request);
    Task<VehicleDto> UpdateVehicleAsync(Guid operatorUserId, Guid vehicleId, UpdateVehicleRequest request);
    Task<VehicleDto> DisableVehicleAsync(Guid operatorUserId, Guid vehicleId);
    Task<VehicleDto> EnableVehicleAsync(Guid operatorUserId, Guid vehicleId);

    Task<PageResult<DriverDto>> ListDriversAsync(Guid operatorUserId, int page, int pageSize);
    Task<DriverDto> CreateDriverAsync(Guid operatorUserId, CreateDriverRequest request);
    Task<DriverDto> UpdateDriverAsync(Guid operatorUserId, Guid driverId, UpdateDriverRequest request);
    Task<DriverDto> DisableDriverAsync(Guid operatorUserId, Guid driverId);
    Task<DriverDto> EnableDriverAsync(Guid operatorUserId, Guid driverId);
}

/// <summary>档案管理基类：权限 + 审计样板（ORG-003 同款）。</summary>
public abstract class TransportAdminServiceBase(
    TransportDbContext db,
    IAuthorizationService authorization,
    IAuditLogService audit)
{
    protected readonly TransportDbContext Db = db;

    protected Guid RequireTenantId() =>
        Db.CurrentTenantId ?? throw new TenantContextMissingException();

    protected async Task RequirePermissionAsync(Guid operatorUserId, string permissionCode, string action)
    {
        if (!await authorization.HasPermissionAsync(operatorUserId, permissionCode))
        {
            throw new PermissionDeniedException(action);
        }
    }

    protected Task RecordAuditAsync(string action, string entityType, Guid? entityId, Guid actor, object detail) =>
        audit.RecordAsync(action, entityType, entityId, actor, detail);

    protected static (int Page, int PageSize, int Skip) NormalizePaging(int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
        return (page, pageSize, (page - 1) * pageSize);
    }
}

public sealed class FleetAdminService(
    TransportDbContext db,
    IAuthorizationService authorization,
    IAuditLogService audit) : TransportAdminServiceBase(db, authorization, audit), IFleetAdminService
{
    public async Task<PageResult<VehicleDto>> ListVehiclesAsync(Guid operatorUserId, int page, int pageSize)
    {
        await RequirePermissionAsync(operatorUserId, TransportPermissionCodes.VehicleRead, "transport.vehicle.read");
        var (p, ps, skip) = NormalizePaging(page, pageSize);
        var total = await Db.Vehicles.CountAsync();
        var items = await Db.Vehicles.OrderBy(v => v.PlateNo).Skip(skip).Take(ps).ToListAsync();
        return new PageResult<VehicleDto>(
            items.Select(v => new VehicleDto(v.Id, v.PlateNo, v.VehicleType, v.MaxLoadKg, v.MaxVolumeM3, StatusName(v.Status), v.Note)).ToList(),
            total, p, ps);
    }

    public async Task<VehicleDto> CreateVehicleAsync(Guid operatorUserId, CreateVehicleRequest request)
    {
        await RequirePermissionAsync(operatorUserId, TransportPermissionCodes.VehicleManage, "transport.vehicle.create");
        var tenantId = RequireTenantId();

        if (await Db.Vehicles.AnyAsync(v => v.PlateNo == request.PlateNo.Trim().ToUpperInvariant()))
        {
            throw new ConflictException($"车牌号（{request.PlateNo.Trim()}）已存在。");
        }

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var vehicle = Vehicle.Create(tenantId, request.PlateNo, request.VehicleType,
            request.MaxLoadKg, request.MaxVolumeM3, DateTime.UtcNow, request.Note, operatorUserId);
        Db.Vehicles.Add(vehicle);
        await Db.SaveChangesAsync();

        await RecordAuditAsync("transport.vehicle.create", "vehicle", vehicle.Id, operatorUserId,
            new { plateNo = vehicle.PlateNo, vehicleType = vehicle.VehicleType });
        scope.Complete();

        return ToDto(vehicle);
    }

    public async Task<VehicleDto> UpdateVehicleAsync(Guid operatorUserId, Guid vehicleId, UpdateVehicleRequest request)
    {
        await RequirePermissionAsync(operatorUserId, TransportPermissionCodes.VehicleManage, "transport.vehicle.update");
        var vehicle = await RequireVehicleAsync(vehicleId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        vehicle.Update(request.VehicleType, request.MaxLoadKg, request.MaxVolumeM3, request.Note, DateTime.UtcNow, operatorUserId);
        await Db.SaveChangesAsync();

        await RecordAuditAsync("transport.vehicle.update", "vehicle", vehicle.Id, operatorUserId,
            new { plateNo = vehicle.PlateNo, vehicleType = vehicle.VehicleType });
        scope.Complete();

        return ToDto(vehicle);
    }

    public async Task<VehicleDto> DisableVehicleAsync(Guid operatorUserId, Guid vehicleId)
    {
        await RequirePermissionAsync(operatorUserId, TransportPermissionCodes.VehicleManage, "transport.vehicle.disable");
        var vehicle = await RequireVehicleAsync(vehicleId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        vehicle.Disable(DateTime.UtcNow, operatorUserId);
        await Db.SaveChangesAsync();
        await RecordAuditAsync("transport.vehicle.disable", "vehicle", vehicle.Id, operatorUserId, new { plateNo = vehicle.PlateNo });
        scope.Complete();

        return ToDto(vehicle);
    }

    public async Task<VehicleDto> EnableVehicleAsync(Guid operatorUserId, Guid vehicleId)
    {
        await RequirePermissionAsync(operatorUserId, TransportPermissionCodes.VehicleManage, "transport.vehicle.enable");
        var vehicle = await RequireVehicleAsync(vehicleId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        vehicle.Enable(DateTime.UtcNow, operatorUserId);
        await Db.SaveChangesAsync();
        await RecordAuditAsync("transport.vehicle.enable", "vehicle", vehicle.Id, operatorUserId, new { plateNo = vehicle.PlateNo });
        scope.Complete();

        return ToDto(vehicle);
    }

    public async Task<PageResult<DriverDto>> ListDriversAsync(Guid operatorUserId, int page, int pageSize)
    {
        await RequirePermissionAsync(operatorUserId, TransportPermissionCodes.VehicleRead, "transport.vehicle.read");
        var (p, ps, skip) = NormalizePaging(page, pageSize);
        var total = await Db.Drivers.CountAsync();
        var items = await Db.Drivers.OrderBy(d => d.Name).Skip(skip).Take(ps).ToListAsync();
        return new PageResult<DriverDto>(
            items.Select(d => new DriverDto(d.Id, d.Name, d.Phone, d.LicenseNo, StatusName(d.Status), d.Note)).ToList(),
            total, p, ps);
    }

    public async Task<DriverDto> CreateDriverAsync(Guid operatorUserId, CreateDriverRequest request)
    {
        await RequirePermissionAsync(operatorUserId, TransportPermissionCodes.VehicleManage, "transport.driver.create");
        var tenantId = RequireTenantId();

        if (await Db.Drivers.AnyAsync(d => d.LicenseNo == request.LicenseNo.Trim().ToUpperInvariant()))
        {
            throw new ConflictException($"驾驶证号（{request.LicenseNo.Trim()}）已存在。");
        }

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var driver = Driver.Create(tenantId, request.Name, request.Phone, request.LicenseNo, DateTime.UtcNow, request.Note, operatorUserId);
        Db.Drivers.Add(driver);
        await Db.SaveChangesAsync();

        await RecordAuditAsync("transport.driver.create", "driver", driver.Id, operatorUserId,
            new { name = driver.Name, licenseNo = driver.LicenseNo });
        scope.Complete();

        return ToDto(driver);
    }

    public async Task<DriverDto> UpdateDriverAsync(Guid operatorUserId, Guid driverId, UpdateDriverRequest request)
    {
        await RequirePermissionAsync(operatorUserId, TransportPermissionCodes.VehicleManage, "transport.driver.update");
        var driver = await RequireDriverAsync(driverId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        driver.Update(request.Name, request.Phone, request.Note, DateTime.UtcNow, operatorUserId);
        await Db.SaveChangesAsync();
        await RecordAuditAsync("transport.driver.update", "driver", driver.Id, operatorUserId, new { name = driver.Name });
        scope.Complete();

        return ToDto(driver);
    }

    public async Task<DriverDto> DisableDriverAsync(Guid operatorUserId, Guid driverId)
    {
        await RequirePermissionAsync(operatorUserId, TransportPermissionCodes.VehicleManage, "transport.driver.disable");
        var driver = await RequireDriverAsync(driverId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        driver.Disable(DateTime.UtcNow, operatorUserId);
        await Db.SaveChangesAsync();
        await RecordAuditAsync("transport.driver.disable", "driver", driver.Id, operatorUserId, new { name = driver.Name });
        scope.Complete();

        return ToDto(driver);
    }

    public async Task<DriverDto> EnableDriverAsync(Guid operatorUserId, Guid driverId)
    {
        await RequirePermissionAsync(operatorUserId, TransportPermissionCodes.VehicleManage, "transport.driver.enable");
        var driver = await RequireDriverAsync(driverId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        driver.Enable(DateTime.UtcNow, operatorUserId);
        await Db.SaveChangesAsync();
        await RecordAuditAsync("transport.driver.enable", "driver", driver.Id, operatorUserId, new { name = driver.Name });
        scope.Complete();

        return ToDto(driver);
    }

    private async Task<Vehicle> RequireVehicleAsync(Guid vehicleId) =>
        await Db.Vehicles.FindAsync([vehicleId]) ?? throw new EntityNotFoundException("车辆", vehicleId);

    private async Task<Driver> RequireDriverAsync(Guid driverId) =>
        await Db.Drivers.FindAsync([driverId]) ?? throw new EntityNotFoundException("司机", driverId);

    private static string StatusName(short status) => status == 0 ? "Active" : "Disabled";

    private static VehicleDto ToDto(Vehicle v) => new(
        v.Id, v.PlateNo, v.VehicleType, v.MaxLoadKg, v.MaxVolumeM3, StatusName(v.Status), v.Note);

    private static DriverDto ToDto(Driver d) => new(
        d.Id, d.Name, d.Phone, d.LicenseNo, StatusName(d.Status), d.Note);
}
