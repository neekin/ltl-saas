namespace Ltl.Modules.Platform.Application;

/// <summary>文件存储配置（FILE-001）。生产密钥类配置经环境变量注入（AGENTS §11）。</summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>本地存储根目录（开发实现）。</summary>
    public string LocalBasePath { get; set; } = "App_Data/files";

    /// <summary>单文件大小上限（字节，默认 20MB）。</summary>
    public long MaxUploadBytes { get; set; } = 20 * 1024 * 1024;
}
