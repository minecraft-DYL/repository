using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Wyolm.Core.Services;

namespace Wyolm.App;

/// <summary>应用入口。启动时先做一次崩溃恢复，再把界面亮出来。</summary>
public partial class App : Application
{
    /// <summary>主窗口，供文件选择器等需要 HWND 的地方使用。</summary>
    public static Window? MainWindow { get; private set; }

    /// <summary>本次启动的崩溃恢复报告，由「历史与恢复」页展示。</summary>
    public static RecoveryReport? StartupRecovery { get; private set; }

    public App()
    {
        InitializeComponent();

        UnhandledException += (_, e) => LogCrash("未处理异常", e.Exception);
    }

    /// <summary>
    /// 把异常追加到 <c>%LOCALAPPDATA%\Wyolm\logs\crash.log</c>。
    /// <para>注意：这里只是留证据。异常从 WinRT 事件处理器里逃出去时，
    /// XAML 会把它当成致命错误结束进程 —— 想让它活下来，必须在事件处理器内部就 catch 住。</para>
    /// </summary>
    public static void LogCrash(string context, Exception? ex)
    {
        try
        {
            AppPaths.EnsureCreated();
            var file = Path.Combine(AppPaths.LogDirectory, "crash.log");
            File.AppendAllText(file,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}{Environment.NewLine}{ex}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // 日志都写不下去的时候，也没别的办法了。
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs e)
    {
        var window = new MainWindow();
        MainWindow = window;
        window.Activate();

        // 恢复逻辑要遍历磁盘，放到后台做，避免界面迟迟不出来。
        _ = RunStartupRecoveryAsync(window);
    }

    private static async Task RunStartupRecoveryAsync(Window window)
    {
        try
        {
            var report = await Task.Run(() => RecoveryService.Recover());
            StartupRecovery = report;

            MainWindow?.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, async () =>
            {
                try
                {
                    if (!report.HasWork) return;

                    var dialog = new ContentDialog
                    {
                        Title = "发现上次中断的搬迁",
                        Content = BuildRecoveryMessage(report),
                        CloseButtonText = "知道了",
                        XamlRoot = window.Content?.XamlRoot,
                    };

                    await dialog.ShowAsync();
                }
                catch (Exception)
                {
                    // 弹窗失败不影响主流程，用户仍可在「历史与恢复」页看到结果。
                }
            });
        }
        catch (Exception ex)
        {
            try { StartupRecovery = new RecoveryReport { Items = [] }; } catch (Exception) { }
            System.Diagnostics.Debug.WriteLine("启动恢复失败：" + ex);
        }
    }

    private static string BuildRecoveryMessage(RecoveryReport report)
    {
        var lines = new List<string> { report.Summary, string.Empty };
        foreach (var item in report.Items.Take(8))
        {
            lines.Add($"· [{item.Action}] {item.Detail}");
        }

        if (report.Items.Count > 8)
            lines.Add($"……还有 {report.Items.Count - 8} 条，详见「历史与恢复」页。");

        return string.Join(Environment.NewLine, lines);
    }
}
