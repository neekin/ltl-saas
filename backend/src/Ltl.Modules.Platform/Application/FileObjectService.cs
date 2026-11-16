using System.Text.RegularExpressions;
using Ltl.Modules.Platform.Domain;
using Ltl.Modules.Platform.Infrastructure;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ltl.Modules.Platform.Application;

public sealed record FileObjectDto(
    Guid Id, string FileName, string ContentType, long SizeBytes, string StorageProvider, DateTime CreatedAt);

public interface IFileObjectService
{
    /// <summary>上传文件（流式写入存储适配器 + 落元数据）。</summary>
    /// <exception cref="ArgumentException">文件为空或超过大小上限。</exception>
    Task<FileObjectDto> UploadAsync(Guid operatorUserId, string fileName, string contentType, Stream content, long sizeBytes);

    /// <summary>元数据（租户过滤；跨租户 → 404 语义）。</summary>
    Task<FileObjectDto> GetAsync(Guid fileId);

    /// <summary>打开下载流；文件在存储中缺失 → EntityNotFoundException。</summary>
    Task<(FileObjectDto Metadata, Stream Content)> GetContentAsync(Guid fileId);
}

/// <summary>
/// 文件对象编排（FILE-001）：存储键由本服务生成（含租户前缀 + 扩展名白名单化，防路径穿越），
/// 内容经 IFileStorage 抽象写入；元数据唯一（ux_file_objects_storage_key）。
/// </summary>
public sealed class FileObjectService(
    PlatformDbContext db,
    IFileStorage storage,
    IOptions<FileStorageOptions> options) : IFileObjectService
{
    private static readonly Regex SafeExtension = new(
        @"^[a-zA-Z0-9]{0,10}(\.[a-zA-Z0-9]{1,10})*$", RegexOptions.Compiled);

    public async Task<FileObjectDto> UploadAsync(
        Guid operatorUserId, string fileName, string contentType, Stream content, long sizeBytes)
    {
        var tenantId = db.CurrentTenantId
            ?? throw new TenantContextMissingException();

        if (sizeBytes <= 0)
        {
            throw new ArgumentException("文件不能为空。", nameof(content));
        }

        if (sizeBytes > options.Value.MaxUploadBytes)
        {
            throw new ArgumentException(
                $"文件大小超过上限（{options.Value.MaxUploadBytes / 1024 / 1024}MB）。", nameof(content));
        }

        var extension = ExtractSafeExtension(fileName);
        var now = DateTime.UtcNow;
        var storageKey = $"{tenantId:N}/{now:yyyyMM}/{Guid.NewGuid():N}{extension}";

        var written = await storage.SaveAsync(storageKey, content);
        if (written != sizeBytes)
        {
            throw new InvalidOperationException(
                $"存储写入字节数（{written}）与上传声明（{sizeBytes}）不一致。");
        }

        var fileObject = FileObject.Create(
            tenantId, fileName, contentType, written, storage.ProviderName, storageKey, operatorUserId, now);
        db.FileObjects.Add(fileObject);
        await db.SaveChangesAsync();

        return Map(fileObject);
    }

    public async Task<FileObjectDto> GetAsync(Guid fileId)
    {
        var fileObject = await db.FileObjects.FindAsync([fileId])
            ?? throw new EntityNotFoundException("文件", fileId);
        return Map(fileObject);
    }

    public async Task<(FileObjectDto Metadata, Stream Content)> GetContentAsync(Guid fileId)
    {
        var fileObject = await db.FileObjects.FindAsync([fileId])
            ?? throw new EntityNotFoundException("文件", fileId);

        var stream = await storage.GetAsync(fileObject.StorageKey)
            ?? throw new EntityNotFoundException("文件内容", fileId);
        return (Map(fileObject), stream);
    }

    /// <summary>提取扩展名（白名单化：仅字母数字与点，总长 ≤11），无扩展名返回空串。</summary>
    private static string ExtractSafeExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName.Trim());
        return string.IsNullOrEmpty(extension) || !SafeExtension.IsMatch(extension)
            ? string.Empty
            : extension.ToLowerInvariant();
    }

    private static FileObjectDto Map(FileObject fileObject) => new(
        fileObject.Id, fileObject.FileName, fileObject.ContentType,
        fileObject.SizeBytes, fileObject.StorageProvider, fileObject.CreatedAt);
}
