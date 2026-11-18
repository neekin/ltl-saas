namespace Ltl.Modules.Transport.Domain;

/// <summary>
/// 运输批次 GPS 轨迹点（GPS-001，追加式）。移动端在批次 Departed 期间上报浏览器定位；
/// 记录不可修改删除（同 AGENTS §6.5 追加式口径）。
/// </summary>
public sealed class TripGpsPoint : TenantScopedEntity
{
    public Guid TripId { get; private set; }

    /// <summary>纬度（WGS84；NUMERIC(9,6)）。</summary>
    public decimal Latitude { get; private set; }

    /// <summary>经度（WGS84；NUMERIC(9,6)）。</summary>
    public decimal Longitude { get; private set; }

    /// <summary>定位精度（米，可空——浏览器 Geolocation 提供）。</summary>
    public decimal? AccuracyMeters { get; private set; }

    /// <summary>定位时刻（UTC）。</summary>
    public DateTime RecordedAt { get; private set; }

    /// <summary>上报操作人。</summary>
    public Guid? RecordedBy { get; private set; }

    private TripGpsPoint()
    {
    }

    /// <exception cref="ArgumentException">坐标非法时抛出。</exception>
    public static TripGpsPoint Create(
        Guid tenantId, Guid tripId, decimal latitude, decimal longitude,
        decimal? accuracyMeters, DateTime recordedAtUtc, Guid? recordedBy)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("租户 Id 不能为空 Guid。", nameof(tenantId));
        }
        if (tripId == Guid.Empty)
        {
            throw new ArgumentException("批次 Id 不能为空 Guid。", nameof(tripId));
        }
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentException("纬度必须在 [-90, 90]。", nameof(latitude));
        }
        if (longitude is < -180 or > 180)
        {
            throw new ArgumentException("经度必须在 [-180, 180]。", nameof(longitude));
        }
        if (accuracyMeters is < 0)
        {
            throw new ArgumentException("定位精度不能为负数。", nameof(accuracyMeters));
        }

        return new TripGpsPoint
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TripId = tripId,
            Latitude = latitude,
            Longitude = longitude,
            AccuracyMeters = accuracyMeters,
            RecordedAt = recordedAtUtc,
            RecordedBy = recordedBy,
        };
    }
}
