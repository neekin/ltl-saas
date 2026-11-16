namespace Ltl.Modules.Platform.Application;

/// <summary>
/// 对象存储抽象（AGENTS §2：文件服务通过抽象接口接入）。
/// 开发实现为本地文件系统（LocalFileStorage）；生产 S3 兼容实现（P9）实现同一接口，调用方零改动。
/// key 由调用方（FileObjectService）生成；实现不得使用 key 拼接用户输入路径。
/// </summary>
public interface IFileStorage
{
    /// <summary>提供者名称（持久化到 file_objects.storage_provider）。</summary>
    string ProviderName { get; }

    /// <summary>写入文件内容；返回实际写入字节数。</summary>
    Task<long> SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default);

    /// <summary>读取文件内容；不存在返回 null。</summary>
    Task<Stream?> GetAsync(string storageKey, CancellationToken cancellationToken = default);
}
