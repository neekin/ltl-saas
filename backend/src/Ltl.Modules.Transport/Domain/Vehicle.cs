namespace Ltl.Modules.Transport.Domain;

/// <summary>车辆档案（VEHICLE-001）。车牌租户内唯一；载重/容积上限供配载校验（TRIP-001）。</summary>
public sealed class Vehicle : TenantScopedEntity
{
    public string PlateNo { get; private set; } = null!;

    /// <summary>车型（van/truck 等业务约定字符串）。</summary>
    public string VehicleType { get; private set; } = null!;

    /// <summary>最大载重（kg）。</summary>
    public decimal MaxLoadKg { get; private set; }

    /// <summary>最大容积（m³）。</summary>
    public decimal MaxVolumeM3 { get; private set; }

    /// <summary>0=Active 1=Disabled（与 Organization 一致）。</summary>
    public short Status { get; private set; }

    public string? Note { get; private set; }

    private Vehicle()
    {
    }

    public static Vehicle Create(
        Guid tenantId, string plateNo, string vehicleType,
        decimal maxLoadKg, decimal maxVolumeM3,
        DateTime nowUtc, string? note = null, Guid? createdBy = null)
    {
        if (maxLoadKg <= 0)
        {
            throw new ArgumentException("最大载重必须 >0。", nameof(maxLoadKg));
        }

        if (maxVolumeM3 <= 0)
        {
            throw new ArgumentException("最大容积必须 >0。", nameof(maxVolumeM3));
        }

        return new Vehicle
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            PlateNo = RequireNonBlank(plateNo, "车牌号", 20).ToUpperInvariant(),
            VehicleType = RequireNonBlank(vehicleType, "车型", 20).ToLowerInvariant(),
            MaxLoadKg = maxLoadKg,
            MaxVolumeM3 = maxVolumeM3,
            Status = 0,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAt = EnsureUtc(nowUtc),
            CreatedBy = createdBy,
        };
    }

    /// <summary>编辑车辆档案（车牌不可变；上限可调）。</summary>
    public void Update(string vehicleType, decimal maxLoadKg, decimal maxVolumeM3, string? note, DateTime nowUtc, Guid? updatedBy = null)
    {
        if (maxLoadKg <= 0)
        {
            throw new ArgumentException("最大载重必须 >0。", nameof(maxLoadKg));
        }

        if (maxVolumeM3 <= 0)
        {
            throw new ArgumentException("最大容积必须 >0。", nameof(maxVolumeM3));
        }

        VehicleType = RequireNonBlank(vehicleType, "车型", 20).ToLowerInvariant();
        MaxLoadKg = maxLoadKg;
        MaxVolumeM3 = maxVolumeM3;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        Touch(nowUtc, updatedBy);
    }

    public void Disable(DateTime nowUtc, Guid? updatedBy = null)
    {
        if (Status == 1)
        {
            return;
        }

        Status = 1;
        Touch(nowUtc, updatedBy);
    }

    public void Enable(DateTime nowUtc, Guid? updatedBy = null)
    {
        if (Status == 0)
        {
            return;
        }

        Status = 0;
        Touch(nowUtc, updatedBy);
    }
}
