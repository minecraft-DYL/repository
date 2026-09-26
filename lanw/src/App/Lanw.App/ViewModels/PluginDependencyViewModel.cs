namespace Lanw.App.ViewModels;

/// <summary>
/// 插件依赖项（对应原 Vue PluginDetail.vue 里 dependencies 渲染的依赖链接 +
/// Lanw.Public.Entities.Plugin.EntityPluginDependency）。
/// </summary>
public sealed class PluginDependencyViewModel
{
    public PluginDependencyViewModel(string id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>依赖插件 ID（点击可跳转到该插件的详情页）。</summary>
    public string Id { get; }

    /// <summary>依赖插件名称。</summary>
    public string Name { get; }
}
