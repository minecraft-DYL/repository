using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Wyolm.App.Views;
using Wyolm.Core.Services;

namespace Wyolm.App;

/// <summary>主窗口：左侧导航 + 右侧内容区。</summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        try
        {
            AppWindow.Resize(new Windows.Graphics.SizeInt32(1180, 780));
        }
        catch (Exception)
        {
            // 某些环境下 AppWindow 不可用，让系统给默认尺寸即可。
        }

        NavigateTo(typeof(ScanPage));
        Nav.SelectedItem = Nav.MenuItems[0];

        _ = ShowCapabilityAsync();
    }

    /// <summary>链接能力探测要碰一下文件系统，放到后台做。</summary>
    private async Task ShowCapabilityAsync()
    {
        var text = await Task.Run(LinkService.DescribeCapability);
        CapabilityText.Text = text;
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;

        var pageType = item.Tag switch
        {
            "scan" => typeof(ScanPage),
            "reconnect" => typeof(ReconnectPage),
            "custom" => typeof(CustomPage),
            "history" => typeof(HistoryPage),
            _ => typeof(ScanPage),
        };

        NavigateTo(pageType);
    }

    /// <summary>
    /// 导航到指定页面。
    /// <para>页面构造里任何异常（XAML 解析、初始化顺序、绑定）默认会走 WinRT 的
    /// 未处理异常路径，直接以 0xc000027b 结束整个进程 —— 用户看到的就是"程序闪退"。
    /// 一个页面的问题不该让另外三个页面一起陪葬，所以这里兜住并把原因显示出来。</para>
    /// </summary>
    private void NavigateTo(Type pageType)
    {
        try
        {
            if (ContentFrame.CurrentSourcePageType != pageType)
                ContentFrame.Navigate(pageType);
        }
        catch (Exception ex)
        {
            try
            {
                App.LogCrash($"导航到 {pageType.Name} 失败", ex);
            }
            catch (Exception)
            {
                // 记日志本身失败也不能影响后面的提示。
            }

            _ = ShowNavigationFailureAsync(pageType, ex);
        }
    }

    private async Task ShowNavigationFailureAsync(Type pageType, Exception ex)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = "这个页面打不开",
                Content = $"{pageType.Name} 初始化时出错，其它页面仍然可用。\n\n{ex.GetType().Name}: {ex.Message}",
                CloseButtonText = "知道了",
                XamlRoot = Content?.XamlRoot,
            };
            await dialog.ShowAsync();
        }
        catch (Exception)
        {
            // 连提示都弹不出来（例如 XamlRoot 还没有），至少日志里已经有记录了。
        }
    }
}
