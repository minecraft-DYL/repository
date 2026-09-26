using System.Reflection;
using Lanw.Core.Utils;

namespace Lanw.Public.Entities.Plugin;

/// <summary>插件程序集（含 SHA256 指纹，用于判断是否需要重新加载），由 Nirvana.Public.Entities.Plugin.EntityPluginAssembly 移植。</summary>
public class EntityPluginAssembly(string pluginPath, Assembly assembly) {
    private readonly string _sha256 = Tools.ComputeSha256(pluginPath);

    public readonly Assembly Assembly = assembly;

    public bool Equals(string pluginPath)
    {
        return _sha256.Equals(Tools.ComputeSha256(pluginPath));
    }
}
