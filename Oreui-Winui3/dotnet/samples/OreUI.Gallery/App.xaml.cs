using Microsoft.UI.Xaml;

namespace OreUI.Gallery;

/// <summary>OreUI.WinUI 组件总览示例应用。</summary>
public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "gallery-errors.log");

    private Window? _window;

    /// <summary>创建应用实例。</summary>
    public App()
    {
        UnhandledException += (_, e) => Log("UNHANDLED", e.Exception);

        // 关键诊断：WinUI 的 STOWED_EXCEPTION(0xc000027b) 会被运行时“收起”，
        // Application.UnhandledException 收不到，只留下一条 fail-fast 崩溃记录
        // （Faulting module: Microsoft.UI.Xaml.dll / Exception code: 0xc000027b）。
        // FirstChanceException 能在异常第一次抛出的瞬间抓到它，于是日志里就有真实堆栈了。
        // 按 (类型, HResult, 首帧) 去重，避免被运行时内部正常吞掉的异常刷屏。
        var seen = new HashSet<string>(StringComparer.Ordinal);
        AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
        {
            var ex = e.Exception;
            var frame = ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim() ?? string.Empty;
            var signature = $"{ex.GetType().FullName}|0x{ex.HResult:X8}|{frame}";

            lock (seen)
            {
                if (!seen.Add(signature))
                {
                    return;
                }
            }

            Log("FIRSTCHANCE", ex);
        };

        InitializeComponent();
    }

    /// <inheritdoc/>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _window = new MainWindow();
            _window.Activate();
        }
        catch (Exception ex)
        {
            // WinUI 的 XAML 解析异常堆栈长、且默认弹窗信息很少；
            // 落盘一份日志，排查 Gallery 启动失败时直接看程序目录下的这个文件。
            Log("LAUNCH", ex);
            throw;
        }
    }

    private static void Log(string kind, Exception ex)
    {
        try
        {
            File.AppendAllText(
                LogPath,
                $"==== {kind} ====\n{ex.GetType().FullName}: {ex.Message}\nHResult=0x{ex.HResult:X8}\n{ex.StackTrace}\n\n");
        }
        catch
        {
            // 诊断日志失败不应影响应用行为
        }
    }
}
