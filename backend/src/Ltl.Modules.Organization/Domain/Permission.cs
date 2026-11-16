namespace Ltl.Modules.Organization.Domain;

/// <summary>权限点（平台全局表，非租户范围）。清单由迁移预置，模块内命名规范：模块.资源.动作。</summary>
public sealed class Permission
{
    public Guid Id { get; private set; }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string Module { get; private set; } = null!;

    private Permission()
    {
    }

    public static Permission Create(string code, string name, string module, Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("权限编码不能为空。", nameof(code));
        }

        return new Permission
        {
            Id = id ?? Guid.NewGuid(),
            Code = code.Trim(),
            Name = RequireNonBlankName(name),
            Module = RequireNonBlankModule(module),
        };
    }

    private static string RequireNonBlankName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("权限名称不能为空。", nameof(name));
        }

        return name.Trim();
    }

    private static string RequireNonBlankModule(string module)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            throw new ArgumentException("模块名不能为空。", nameof(module));
        }

        return module.Trim();
    }
}
