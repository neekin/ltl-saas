namespace Ltl.Modules.Platform.Domain;

/// <summary>
/// 文件对象元数据（file_objects，FILE-001）。文件内容存于对象存储（抽象接口），
/// 数据库仅存元数据；storage_key 由服务生成（含租户前缀，全局唯一）。
/// </summary>
public sealed class FileObject : TenantScopedEntity
{
    /// <summary>原始文件名。</summary>
    public string FileName { get; private set; } = null!;

    public string ContentType { get; private set; } = null!;

    public long SizeBytes { get; private set; }

    /// <summary>存储提供者（local / s3）。</summary>
    public string StorageProvider { get; private set; } = null!;

    /// <summary>存储键（全局唯一，含租户前缀）。</summary>
    public string StorageKey { get; private set; } = null!;

    public Guid? UploadedBy { get; private set; }

    private FileObject()
    {
    }

    public static FileObject Create(
        Guid tenantId,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageProvider,
        string storageKey,
        Guid? uploadedBy,
        DateTime createdAtUtc)
    {
        var created = EnsureUtc(createdAtUtc);

        if (sizeBytes <= 0)
        {
            throw new ArgumentException("文件大小必须大于 0。", nameof(sizeBytes));
        }

        return new FileObject
        {
            Id = Guid.NewGuid(),
            TenantId = RequireTenantId(tenantId),
            FileName = RequireNonBlank(fileName, "文件名", 200),
            ContentType = RequireNonBlank(contentType, "Content-Type", 100),
            SizeBytes = sizeBytes,
            StorageProvider = RequireNonBlank(storageProvider, "存储提供者", 20),
            StorageKey = RequireNonBlank(storageKey, "存储键", 200),
            UploadedBy = uploadedBy,
            CreatedAt = created,
        };
    }
}
