using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lanw.DevPlugin.Entities;

namespace Lanw.App.ViewModels;

/// <summary>
/// 已安装插件列表项（对应原 Vue plugin/Plugins.vue 表格的一行 + Lanw.DevPlugin.Entities.EntityPluginState）。
/// 启用开关对应原 Vue 的「停止/启动」按钮（PluginManager.TogglePlugin），删除对应 DeletePlugin。
/// </summary>
public sealed partial class PluginItemViewModel : ObservableObject
{
    public PluginItemViewModel(
        EntityPluginState state,
        IAsyncRelayCommand<PluginItemViewModel> toggleCommand,
        IAsyncRelayCommand<PluginItemViewModel> deleteCommand)
    {
        Id = state.Id;
        Name = state.Name;
        Version = state.Version;
        Author = state.Author;
        Path = state.Path;
        // EntityPluginState.Status 由 PluginState.IsEnabled 生成："1" 启用 / "0" 停用
        IsEnabled = state.Status == "1";
        ToggleCommand = toggleCommand;
        DeleteCommand = deleteCommand;
    }

    /// <summary>插件 ID（Plugin.Info.Id）。</summary>
    public string Id { get; }

    /// <summary>插件名称（Plugin.Info.Name）。</summary>
    public string Name { get; }

    /// <summary>插件版本（Plugin.Info.Version）。</summary>
    public string Version { get; }

    /// <summary>插件作者（Plugin.Info.Author）。</summary>
    public string Author { get; }

    /// <summary>插件文件路径（停用中的插件以 .disable 结尾）。</summary>
    public string Path { get; }

    /// <summary>启用状态（EntityPluginState.Status == "1" 即「启动」）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(ToggleText))]
    public partial bool IsEnabled { get; set; }

    /// <summary>状态列文案（对应 Vue status == 1 ? '启动' : '未启动'）。</summary>
    public string StatusText => IsEnabled ? "启动" : "未启动";

    /// <summary>操作按钮文案（对应 Vue status == 1 ? '停止' : '启动'）。</summary>
    public string ToggleText => IsEnabled ? "停止" : "启动";

    /// <summary>启用/停用命令（由 PluginsViewModel 提供，作用于本行）。</summary>
    public IAsyncRelayCommand<PluginItemViewModel> ToggleCommand { get; }

    /// <summary>删除命令（由 PluginsViewModel 提供，作用于本行）。</summary>
    public IAsyncRelayCommand<PluginItemViewModel> DeleteCommand { get; }
}
