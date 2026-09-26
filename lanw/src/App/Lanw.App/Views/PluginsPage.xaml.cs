using Lanw.App.Services;
using Lanw.App.ViewModels;
using Microsoft.UI.Xaml.Navigation;

namespace Lanw.App.Views;

/// <summary>
/// 插件管理页（对应原 Vue plugin/Plugins.vue）：已安装插件列表（名称 / 版本 / 作者 / 状态）、
/// 启用开关（PluginManager.TogglePlugin）、删除（DeletePlugin，删除前弹确认框）、
/// 自动更新开关（LanwConfig.autoUpdatePlugin）。数据来自进程内直调的 PluginService，无 HTTP 中转。
/// 主题跟随系统（ThemeService.FollowSystemTheme）。
/// </summary>
public sealed partial class PluginsPage : Page
{
    public PluginsViewModel ViewModel { get; } = new();

    public PluginsPage()
    {
        this.InitializeComponent();
        ThemeService.FollowSystemTheme(this);
    }

    /// <summary>进入页面即读取已安装插件列表。</summary>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (ViewModel.LoadCommand.CanExecute(null))
        {
            ViewModel.LoadCommand.Execute(null);
        }
    }

    /// <summary>
    /// 启用开关：只有开关状态与列表项状态不一致时才触发切换，
    /// 避免列表回填（IsOn 由绑定写入）时误触发 toggle。
    /// </summary>
    private void PluginToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggle || toggle.DataContext is not PluginItemViewModel item)
        {
            return;
        }

        if (toggle.IsOn == item.IsEnabled)
        {
            return;
        }

        if (item.ToggleCommand.CanExecute(item))
        {
            item.ToggleCommand.Execute(item);
        }
    }

    /// <summary>删除插件：先弹确认框（对应原 Vue 的「删除确认」Alert：此操作不可恢复）。</summary>
    private async void DeletePlugin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not PluginItemViewModel item)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "删除确认",
            Content = $"确定要删除插件 {item.Name} 吗？此操作不可恢复。",
            PrimaryButtonText = "确认删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (item.DeleteCommand.CanExecute(item))
        {
            item.DeleteCommand.Execute(item);
        }
    }

    /// <summary>跳转到插件商城页。</summary>
    private void OpenStore_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(PluginStorePage));
    }
}
