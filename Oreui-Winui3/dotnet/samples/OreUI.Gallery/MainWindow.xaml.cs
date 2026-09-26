using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OreUI.WinUI;
using Windows.Graphics;

namespace OreUI.Gallery;

/// <summary>组件总览主窗口。</summary>
public sealed partial class MainWindow : Window
{
    private DispatcherTimer? _loadingTimer;

    /// <summary>创建主窗口。</summary>
    public MainWindow()
    {
        InitializeComponent();

        Title = "OreUI.WinUI · 组件总览";
        AppWindow.Resize(new SizeInt32(1280, 900));

        Sidebar.SizeChanged += (_, _) => SyncContentMargin();
        Sidebar.ItemClick += (_, e) => PopHost.Show($"切换到「{e.Item.Text}」", status: OrePopStatus.Success);
        SyncContentMargin();
    }

    /// <summary>宽屏时侧边栏常驻，正文需要让出 238px。</summary>
    private void SyncContentMargin() =>
        ContentRoot.Margin = Sidebar.IsWideMode ? new Thickness(238, 0, 0, 0) : new Thickness(0);

    private void OnShowBlockClick(object sender, RoutedEventArgs e) =>
        PopHost.Show("链接块被点击了", status: OrePopStatus.Success);

    private void OnPopClick(object sender, RoutedEventArgs e)
    {
        PopHost.Show("这是一条普通气泡提示");
        PopHost.Show("成功：操作已完成", status: OrePopStatus.Success);
        PopHost.Show("错误：操作失败", status: OrePopStatus.Error);
        PopHost.Show("进行中：请稍候…", status: OrePopStatus.Process);
        PopHost.Show("调试文本", status: OrePopStatus.DebugText);
    }

    private void OnModalClick(object sender, RoutedEventArgs e) => DemoModal.IsOpen = true;

    private async void OnDialogClick(object sender, RoutedEventArgs e)
    {
        var confirmed = await OreModal.ShowAsync(
            ContentRoot.XamlRoot,
            "确认操作",
            "这是一个通过 OreModal.ShowAsync 弹出的静态弹窗，点击下方按钮返回结果。",
            "确定",
            "取消");

        PopHost.Show(
            confirmed ? "你点击了「确定」" : "你点击了「取消」",
            status: confirmed ? OrePopStatus.Success : OrePopStatus.None);
    }

    private void OnLoadingClick(object sender, RoutedEventArgs e)
    {
        DemoLoading.ErrorMessage = null;
        DemoLoading.Text = "正在加载资源…";
        DemoLoading.IsLoading = true;

        _loadingTimer?.Stop();
        _loadingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _loadingTimer.Tick += (_, _) =>
        {
            _loadingTimer?.Stop();
            DemoLoading.IsLoading = false;
            PopHost.Show("加载完成", status: OrePopStatus.Success);
        };
        _loadingTimer.Start();
    }
}
