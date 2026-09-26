using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lanw.App.Services;

namespace Lanw.App.ViewModels;

/// <summary>
/// 日志信息页视图模型（对应原 Vue Logs.vue + /api/logs）。
/// 数据链路：进程内直读 t17 移植的后端内存日志 InMemorySink.GetLogs()（经 ViewLogService），
/// 定时刷新（页面持有 DispatcherQueueTimer）+ 级别着色，无 HTTP。
/// </summary>
public sealed partial class LogsViewModel : ObservableObject
{
    /// <summary>界面上保留的最大日志条数（超出丢弃最旧的）。</summary>
    private const int MaxLines = 1000;

    /// <summary>已展示的日志条数（用于增量追加，避免每次刷新重建列表导致滚动位置丢失）。</summary>
    private int _shownCount;

    /// <summary>已展示的第一条日志（用于识别「日志被清空后重新写入」的情况）。</summary>
    private string? _firstShown;

    private bool _lightPalette;

    /// <summary>日志列表（最早在上、最新在下，与参考源倒序输出的顺序一致）。</summary>
    public ObservableCollection<LogItemViewModel> Logs { get; } = [];

    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    [ObservableProperty]
    public partial Visibility EmptyVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility ListVisibility { get; set; }

    /// <summary>是否自动刷新（1 秒一次，实时日志流）。</summary>
    [ObservableProperty]
    public partial bool IsAutoRefresh { get; set; }

    public LogsViewModel()
    {
        StatusMessage = "暂无日志信息";
        EmptyVisibility = Visibility.Visible;
        ListVisibility = Visibility.Collapsed;
        IsAutoRefresh = true;

        // 进入即接入内存日志（幂等），确保日志页能看到各后端模块的 Log.* 输出
        ViewLogService.Attach();
        RefreshFromSink();
    }

    /// <summary>按当前主题切换配色（亮色下调深，保证可读性）。</summary>
    public void ApplyTheme(bool lightPalette)
    {
        if (_lightPalette == lightPalette)
        {
            return;
        }

        _lightPalette = lightPalette;
        // 配色是随行固化的，需重建列表才能整体换色
        Rebuild(ViewLogService.GetLogs());
    }

    /// <summary>手动刷新（对应参考源页面加载 / 刷新按钮）。</summary>
    [RelayCommand]
    private void Refresh()
    {
        RefreshFromSink();
        StatusMessage = Logs.Count == 0 ? "暂无日志信息" : $"已刷新，共 {Logs.Count} 条日志";
    }

    /// <summary>清空内存日志（InMemorySink.Clear 的桌面安全封装）。</summary>
    [RelayCommand]
    private void Clear()
    {
        ViewLogService.Clear();
        Logs.Clear();
        _shownCount = 0;
        _firstShown = null;
        UpdateStatus();
        StatusMessage = "已清空日志信息";
    }

    /// <summary>从内存日志增量同步到界面（页面定时器每秒调用）。</summary>
    public void RefreshFromSink()
    {
        var all = ViewLogService.GetLogs();

        var cleared = all.Count == 0
            || _shownCount > all.Count
            || (_shownCount > 0 && !string.Equals(all[0], _firstShown, StringComparison.Ordinal));

        if (cleared)
        {
            Rebuild(all);
        }
        else if (all.Count > _shownCount)
        {
            for (var i = _shownCount; i < all.Count; i++)
            {
                Logs.Add(new LogItemViewModel(all[i], _lightPalette));
            }

            _shownCount = all.Count;
            TrimOldest();
        }

        UpdateStatus();
    }

    private void Rebuild(IReadOnlyList<string> all)
    {
        Logs.Clear();
        foreach (var line in all)
        {
            Logs.Add(new LogItemViewModel(line, _lightPalette));
        }

        _shownCount = all.Count;
        _firstShown = all.Count > 0 ? all[0] : null;
        TrimOldest();
    }

    private void TrimOldest()
    {
        while (Logs.Count > MaxLines)
        {
            Logs.RemoveAt(0);
        }
    }

    private void UpdateStatus()
    {
        var hasLogs = Logs.Count > 0;
        ListVisibility = hasLogs ? Visibility.Visible : Visibility.Collapsed;
        EmptyVisibility = hasLogs ? Visibility.Collapsed : Visibility.Visible;
        StatusMessage = hasLogs
            ? $"共 {Logs.Count} 条日志" + (IsAutoRefresh ? "（实时刷新中）" : "（自动刷新已关闭）")
            : "暂无日志信息";
    }
}
