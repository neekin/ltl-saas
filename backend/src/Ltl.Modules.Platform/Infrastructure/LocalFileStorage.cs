using Ltl.Modules.Platform.Application;
using Microsoft.Extensions.Options;

namespace Ltl.Modules.Platform.Infrastructure;

/// <summary>本地文件系统存储实现（开发环境）。生产使用 S3 兼容适配器（P9）。</summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _basePath;

    public LocalFileStorage(IOptions<FileStorageOptions> options)
    {
        _basePath = options.Value.LocalBasePath;
        ArgumentException.ThrowIfNullOrWhiteSpace(_basePath);
    }

    public string ProviderName => "local";

    public async Task<long> SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var position = content.Position;
        await content.CopyToAsync(file, cancellationToken);
        return content.Position - position;
    }

    public Task<Stream?> GetAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(storageKey);
        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        return Task.FromResult<Stream?>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
    }

    /// <summary>key → 物理路径：key 为服务端生成（tenant/yyyyMM/guid.ext），逐段拼接防路径穿越。</summary>
    private string ResolvePath(string storageKey)
    {
        var segments = storageKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => s is "." or ".." || s.Contains('\\') || Path.IsPathRooted(s)))
        {
            throw new ArgumentException("非法的存储键。", nameof(storageKey));
        }

        return Path.Combine([_basePath, .. segments]);
    }
}
