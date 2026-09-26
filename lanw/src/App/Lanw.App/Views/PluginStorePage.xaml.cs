using Lanw.App.Services;
using Lanw.App.ViewModels;
using Microsoft.UI.Xaml.Navigation;

namespace Lanw.App.Views;

/// <summary>
/// 插件商城页（对应原 Vue plugin/PluginStore.vue）：插件卡片列表 + 搜索 + 安装按钮。
/// 数据来自进程内直调的 PluginService → PlugInstoreMessage.GetPluginList / Install，无 HTTP 中转。
/// 点击卡片进入插件详情页；无网络时显示错误态并可页内重试（不崩溃）。
/// 主题跟随系统（ThemeService.FollowSystemTheme）。
/// </summary>
public sealed partial class PluginStorePage : Page
{
    public PluginStoreViewModel ViewModel { get; } = new();

    public PluginStorePage()
    {
        this.InitializeComponent();
        ThemeService.FollowSystemTheme(this);
    }

    /// <summary>进入页面即拉取商城列表（对应 Vue onMounted → getPluginList）。</summary>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (ViewModel.LoadCommand.CanExecute(null))
        {
            ViewModel.LoadCommand.Execute(null);
        }
    }

    /// <summary>点击卡片进入插件详情（对应 Vue navigateToPlugin(id)）。</summary>
    private void Plugin_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PluginStoreItemViewModel item && !string.IsNullOrEmpty(item.Id))
        {
            Frame.Navigate(typeof(PluginDetailPage), item.Id);
        }
    }

    /// <summary>跳转到已安装插件页。</summary>
    private void OpenInstalled_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(PluginsPage));
    }
}
