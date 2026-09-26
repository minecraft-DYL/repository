using Lanw.App.Services;
using Lanw.App.ViewModels;
using Microsoft.UI.Xaml.Navigation;

namespace Lanw.App.Views;

/// <summary>
/// 插件详情页（对应原 Vue plugin/PluginDetail.vue）：名称 / 插件 ID / 发布者 / 发布时间 / 插件版本 /
/// 下载数量 / 依赖项（EntityPluginDependency，可跳转）/ 插件介绍；Download 安装（含依赖，安装前弹确认框）。
/// 数据来自进程内直调的 PluginService → PlugInstoreMessage.GetPluginDetail / Install，无 HTTP 中转。
/// 主题跟随系统（ThemeService.FollowSystemTheme）。
/// </summary>
public sealed partial class PluginDetailPage : Page
{
    public PluginDetailViewModel ViewModel { get; } = new();

    public PluginDetailPage()
    {
        this.InitializeComponent();
        ThemeService.FollowSystemTheme(this);
    }

    /// <summary>导航参数为插件 ID，进入页面即加载详情。</summary>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        var pluginId = e.Parameter as string;
        if (ViewModel.LoadCommand.CanExecute(pluginId))
        {
            ViewModel.LoadCommand.Execute(pluginId);
        }
    }

    /// <summary>Download：先弹确认框（对应 Vue 的安装提醒 + confirmMessage 依赖清单），确认后安装。</summary>
    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "插件安装提醒",
            Content = ViewModel.ConfirmMessage + "\n\n部分插件需要重启，才会彻底生效。",
            PrimaryButtonText = "确认安装",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (ViewModel.InstallCommand.CanExecute(null))
        {
            ViewModel.InstallCommand.Execute(null);
        }
    }

    /// <summary>点击依赖项跳转到该依赖插件的详情页（对应 Vue 的依赖链接）。</summary>
    private void Dependency_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: PluginDependencyViewModel dependency }
            && !string.IsNullOrEmpty(dependency.Id))
        {
            Frame.Navigate(typeof(PluginDetailPage), dependency.Id);
        }
    }

    /// <summary>错误态重试。</summary>
    private void Retry_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.LoadCommand.CanExecute(ViewModel.PluginId))
        {
            ViewModel.LoadCommand.Execute(ViewModel.PluginId);
        }
    }

    /// <summary>返回：优先回退历史，无历史时回到插件商城。</summary>
    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
            return;
        }

        Frame.Navigate(typeof(PluginStorePage));
    }
}
