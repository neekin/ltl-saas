namespace Ltl.Modules.Transport.Domain;

/// <summary>司机档案（VEHICLE-001）。证号租户内唯一。</summary>
public sealed class Driver : TenantScopedEntity
{
    public string Name { get; private set; } = null!;

    public string Phone { get; private set; } = null!;

    public string LicenseNo { get; private set; } = null!;

    /// <summary>0=Active 1=Disabled。</summary>
    public short Status { get; private set; }

    public string? Note { get; private set; }

    private Driver()
    {
    }

    public static Driver Create(
        Guid tenantId, string name, string phone, string licenseNo,
        DateTime nowUtc, string? note = null, Guid? createdBy = null)
    {
        return new Driver
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            Name = RequireNonBlank(name, "姓名", 100),
            Phone = RequireNonBlank(phone, "电话", 50),
            LicenseNo = RequireNonBlank(licenseNo, "驾驶证号", 64).ToUpperInvariant(),
            Status = 0,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAt = EnsureUtc(nowUtc),
            CreatedBy = createdBy,
        };
    }

    /// <summary>编辑司机档案（证号不可变）。</summary>
    public void Update(string name, string phone, string? note, DateTime nowUtc, Guid? updatedBy = null)
    {
        Name = RequireNonBlank(name, "姓名", 100);
        Phone = RequireNonBlank(phone, "电话", 50);
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
