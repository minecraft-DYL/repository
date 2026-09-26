using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lanw.App.Services;

namespace Lanw.App.ViewModels;

/// <summary>
/// 插件管理页视图模型（对应原 Vue plugin/Plugins.vue + PluginsListController 的
/// /api/plugins/get | toggle | delete）。
/// 数据链路：PluginService → Lanw.Development.Manager.PluginManager（进程内直调，无 HTTP）。
/// 列表列：名称 / 版本 / 作者 / 状态；操作：启用开关（TogglePlugin）、删除（DeletePlugin）。
/// 「自动更新插件」开关复用 LanwConfig 的 autoUpdatePlugin 键（与设置页同一键，
/// 供 PlugInstoreMessage.AutoUpdateCheck 启动时读取）。
/// </summary>
public sealed partial class PluginsViewModel : ObservableObject
{
    private readonly PluginService _service;

    /// <summary>构造期间不写配置，避免载入动作把状态栏刷成「已开启…」。</summary>
    private bool _ready;

    /// <summary>已安装插件（对应 Vue plugins）。</summary>
    public ObservableCollection<PluginItemViewModel> Plugins { get; } = [];

    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>自动更新插件（LanwConfig: autoUpdatePlugin）。</summary>
    [ObservableProperty]
    public partial bool AutoUpdatePlugin { get; set; }

    [ObservableProperty]
    public partial Visibility ListVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility EmptyVisibility { get; set; }

    public PluginsViewModel(PluginService? service = null)
    {
        _service = service ?? new PluginService();
        StatusMessage = "尚未加载插件列表";
        ListVisibility = Visibility.Collapsed;
        EmptyVisibility = Visibility.Collapsed;

        try
        {
            AutoUpdatePlugin = _service.AutoUpdatePlugin;
        }
        catch (Exception)
        {
            AutoUpdatePlugin = true; // 读配置失败时保持与默认值一致
        }

        _ready = true;
    }

    /// <summary>开关变更即写回 LanwConfig（持久化到 resources/nirvanaAccount.json）。</summary>
    partial void OnAutoUpdatePluginChanged(bool value)
    {
        if (!_ready)
        {
            return;
        }

        try
        {
            _service.AutoUpdatePlugin = value;
            StatusMessage = value ? "已开启插件自动更新" : "已关闭插件自动更新";
        }
        catch (Exception e)
        {
            StatusMessage = "保存自动更新开关失败：" + MessageOf(e);
        }
    }

    /// <summary>加载已安装插件列表（/api/plugins/get）。</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        StatusMessage = "正在读取已安装插件…";
        try
        {
            var states = await _service.GetInstalledPluginsAsync().ConfigureAwait(true);
            Plugins.Clear();
            foreach (var state in states)
            {
                Plugins.Add(new PluginItemViewModel(state, ToggleItemCommand, DeleteItemCommand));
            }

            StatusMessage = Plugins.Count == 0
                ? "plugins 目录下没有已安装的插件（可在「插件商城」安装）"
                : $"共 {Plugins.Count} 个已安装插件";
        }
        catch (Exception e)
        {
            // 插件目录/程序集异常不应让启动器崩溃
            Plugins.Clear();
            StatusMessage = "读取插件列表失败：" + MessageOf(e);
        }
        finally
        {
            IsLoading = false;
            UpdateViewState();
        }
    }

    /// <summary>启用/停用插件（/api/plugins/toggle，含依赖联动，由 PluginManager 处理）。</summary>
    [RelayCommand]
    private async Task ToggleItemAsync(PluginItemViewModel? item)
    {
        if (item is null || IsLoading)
        {
            return;
        }

        IsLoading = true;
        StatusMessage = $"正在{(item.IsEnabled ? "停用" : "启用")}插件 {item.Name}…";
        try
        {
            await _service.TogglePluginAsync(item.Id).ConfigureAwait(true);
            // 状态与文件后缀（.disable）相关，重新读取列表以取准确状态
            IsLoading = false;
            await LoadAsync().ConfigureAwait(true);
            StatusMessage = $"插件 {item.Name} 已{(item.IsEnabled ? "停用" : "启用")}（部分插件需重启才彻底生效）";
        }
        catch (Exception e)
        {
            StatusMessage = $"切换插件状态失败：{MessageOf(e)}";
        }
        finally
        {
            IsLoading = false;
            UpdateViewState();
        }
    }

    /// <summary>删除插件（/api/plugins/delete，含依赖联动；确认对话框由页面负责）。</summary>
    [RelayCommand]
    private async Task DeleteItemAsync(PluginItemViewModel? item)
    {
        if (item is null || IsLoading)
        {
            return;
        }

        IsLoading = true;
        StatusMessage = $"正在删除插件 {item.Name}…";
        try
        {
            await _service.DeletePluginAsync(item.Id).ConfigureAwait(true);
            IsLoading = false;
            await LoadAsync().ConfigureAwait(true);
            StatusMessage = $"插件 {item.Name} 已删除（原文件被标记为待删除，重启后自动清理）";
        }
        catch (Exception e)
        {
            StatusMessage = $"删除插件失败：{MessageOf(e)}";
        }
        finally
        {
            IsLoading = false;
            UpdateViewState();
        }
    }

    private void UpdateViewState()
    {
        ListVisibility = Plugins.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyVisibility = !IsLoading && Plugins.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string MessageOf(Exception e)
        => e is AggregateException aggregate && aggregate.InnerException != null
            ? aggregate.InnerException.Message
            : e.Message;
}
