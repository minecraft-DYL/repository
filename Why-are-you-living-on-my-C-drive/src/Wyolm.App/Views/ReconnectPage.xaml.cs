using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Wyolm.App.Services;
using Wyolm.Core.Models;
using Wyolm.Core.Services;

namespace Wyolm.App.Views;

/// <summary>扫描自定义盘符，把搬走的文件夹用链接接回原位置。</summary>
public sealed partial class ReconnectPage : Page
{
    private readonly List<DriveInfoModel> _drives = [];
    private CancellationTokenSource? _cts;

    public ReconnectPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_drives.Count != 0) return;

        _drives.AddRange(DriveService.GetDrives(includeNonReady: false));
        ScanDriveBox.ItemsSource = _drives;

        // 默认选一个非系统盘：被搬走的东西通常躺在那里。
        var target = _drives.FirstOrDefault(d => d.IsReady && !d.IsSystem) ?? _drives.FirstOrDefault(d => d.IsReady);
        if (target is not null)
        {
            ScanDriveBox.SelectedItem = target;
            ScanRootBox.Text = target.RootPath;
        }
    }

    private void ScanDriveBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ScanDriveBox.SelectedItem is DriveInfoModel drive && drive.IsReady)
            ScanRootBox.Text = drive.RootPath;
    }

    private async void BrowseScanRoot_Click(object sender, RoutedEventArgs e)
    {
        var picked = await UiHelpers.PickFolderAsync(ScanRootBox.Text);
        if (!string.IsNullOrWhiteSpace(picked)) ScanRootBox.Text = picked;
    }

    // ==================================================================
    //  扫描
    // ==================================================================
    private async void Find_Click(object sender, RoutedEventArgs e)
    {
        var root = ScanRootBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            ShowResult(InfoBarSeverity.Warning, "找不到要扫描的目录", root ?? string.Empty);
            return;
        }

        _cts = new CancellationTokenSource();
        SetBusy(true);
        ResultBar.IsOpen = false;
        CandidateList.ItemsSource = null;

        try
        {
            var depth = (int)Math.Clamp(MaxDepthBox.Value, 1, 12);

            var relay = new ProgressRelay<ScanProgress>(DispatcherQueue, p =>
                ProgressDetailText.Text = $"已检查 {p.Done} 个目录，找到 {p.Total} 个可接回的文件夹…");

            var found = await DirectoryScanner.ScanForOriginMarkersAsync(root, depth, relay, _cts.Token);
            CandidateList.ItemsSource = found;

            var reconnectable = found.Count(f => !f.OriginAlreadyLinked && !f.OriginOccupied);

            FindStatusText.Text = found.Count == 0
                ? $"在 {root} 下（深度 {depth}）没有找到带来源标记的文件夹。"
                : $"找到 {found.Count} 个被 Wyolm 搬走的文件夹，其中 {reconnectable} 个现在可以接回原位。";

            if (found.Count == 0)
            {
                ShowResult(InfoBarSeverity.Informational, "没有找到可接回的文件夹",
                    "只有由 Wyolm 搬走（或复制）过的文件夹才会留下 .wyolm-origin.json 标记。\n" +
                    "如果这个文件夹是你自己手动搬到别的盘的，请用下方的「手动接回」。");
            }
        }
        catch (Exception ex)
        {
            ShowResult(InfoBarSeverity.Error, "查找失败", ex.Message);
        }
        finally
        {
            SetBusy(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void CancelFind_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        ProgressDetailText.Text = "正在取消…";
    }

    private void CandidateList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = CandidateList.SelectedItems.Cast<ScanItem>().ToList();
        var usable = selected.Count(i => !i.OriginAlreadyLinked && !i.OriginOccupied);

        SelectionText.Text = selected.Count == 0
            ? "尚未选中任何文件夹"
            : $"已选 {selected.Count} 项，其中 {usable} 项可以接回";

        ReconnectButton.IsEnabled = usable > 0;
    }

    // ==================================================================
    //  自动接回
    // ==================================================================
    private async void Reconnect_Click(object sender, RoutedEventArgs e)
    {
        var selected = CandidateList.SelectedItems.Cast<ScanItem>()
            .Where(i => !i.OriginAlreadyLinked && !i.OriginOccupied && !string.IsNullOrWhiteSpace(i.OriginalPath))
            .ToList();

        if (selected.Count == 0) return;

        var confirm = new ContentDialog
        {
            Title = "确认接回原位？",
            Content = $"将在以下 {selected.Count} 个位置建立目录联接，指向它们在别的盘上的真实目录：\n\n" +
                      string.Join("\n", selected.Take(6).Select(i => $"· {i.OriginalPath}")) +
                      (selected.Count > 6 ? $"\n……还有 {selected.Count - 6} 个" : "") +
                      "\n\n不会移动或删除任何数据，只创建链接。",
            PrimaryButtonText = "建立链接",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        _cts = new CancellationTokenSource();
        SetBusy(true, "正在接回…");
        ResultBar.IsOpen = false;

        var results = new List<MigrationResult>();

        try
        {
            for (int i = 0; i < selected.Count; i++)
                results.Add(await RunOneAsync(selected[i], i, selected.Count, _cts.Token));

            ShowReconnectResult(results);
        }
        catch (Exception ex)
        {
            ShowResult(InfoBarSeverity.Error, "接回过程出错", ex.Message);
        }
        finally
        {
            SetBusy(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task<MigrationResult> RunOneAsync(ScanItem item, int index, int total, CancellationToken ct)
    {
        ProgressPhaseText.Text = $"[{index + 1}/{total}] {item.Name}";
        ProgressDetailText.Text = "准备中…";
        ProgressBarControl.IsIndeterminate = false;
        ProgressBarControl.Value = 0;

        var relay = new ProgressRelay<MigrationProgress>(DispatcherQueue, p =>
        {
            ProgressPhaseText.Text = $"[{index + 1}/{total}] {item.Name} · {p.PhaseDisplay}";
            ProgressDetailText.Text = p.Message ?? string.Empty;
        });

        var request = new MigrationRequest
        {
            SourcePath = item.OriginalPath!,
            DestinationPath = item.FullPath,
            Mode = MigrationMode.LinkOnly,
            LinkKind = LinkKind.Junction,
            Kind = MigrationJobKind.Reconnect,
            Label = $"接回原位：{item.Name}",
        };

        return await new MigrationEngine().ExecuteAsync(request, relay, ct);
    }

    private void ShowReconnectResult(List<MigrationResult> results)
    {
        int ok = results.Count(r => r.Succeeded);
        int bad = results.Count - ok;

        var lines = new List<string>();
        foreach (var r in results)
        {
            lines.Add($"· {r.SourcePath} ← {r.DestinationPath}：{r.OutcomeDisplay}");
            if (r.Failure is not null) lines.Add($"    {r.Failure.Operation}：{r.Failure.Message}");
            foreach (var n in r.Notes) lines.Add("    " + n);
        }

        ShowResult(bad == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
            bad == 0 ? $"已接回 {ok} 个文件夹" : $"完成：成功 {ok} 个，失败 {bad} 个",
            string.Join(Environment.NewLine, lines));

        // 已经接好的，状态刷新一下，避免用户重复点击。
        if (CandidateList.ItemsSource is IReadOnlyList<ScanItem> items)
        {
            foreach (var r in results.Where(r => r.Succeeded))
            {
                var match = items.FirstOrDefault(i =>
                    string.Equals(i.OriginalPath, r.SourcePath, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    match.OriginAlreadyLinked = true;
                    match.Suggestion = "已接好";
                }
            }

            CandidateList.ItemsSource = null;
            CandidateList.ItemsSource = items;
        }
    }

    // ==================================================================
    //  手动接回
    // ==================================================================
    private async void BrowseManualReal_Click(object sender, RoutedEventArgs e)
    {
        var picked = await UiHelpers.PickFolderAsync(ManualRealPathBox.Text);
        if (!string.IsNullOrWhiteSpace(picked)) ManualRealPathBox.Text = picked;
    }

    private async void BrowseManualOriginal_Click(object sender, RoutedEventArgs e)
    {
        // 原位置通常还不存在，选择器会失败 —— 那就让用户直接手动输入。
        var picked = await UiHelpers.PickFolderAsync(ManualOriginalPathBox.Text);
        if (!string.IsNullOrWhiteSpace(picked)) ManualOriginalPathBox.Text = picked;
        else ManualHintText.Text = "原位置如果还不存在，直接手动输入完整路径即可（例如 C:\\Users\\你\\.nuget）。";
    }

    private async void ManualLink_Click(object sender, RoutedEventArgs e)
    {
        var real = ManualRealPathBox.Text?.Trim();
        var original = ManualOriginalPathBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(real) || string.IsNullOrWhiteSpace(original))
        {
            ShowResult(InfoBarSeverity.Warning, "信息不完整", "请同时填写「真实位置」和「原位置」。");
            return;
        }

        _cts = new CancellationTokenSource();
        SetBusy(true, "正在建立链接…");

        try
        {
            var relay = new ProgressRelay<MigrationProgress>(DispatcherQueue, p =>
            {
                ProgressPhaseText.Text = p.PhaseDisplay;
                ProgressDetailText.Text = p.Message ?? string.Empty;
            });

            var request = new MigrationRequest
            {
                SourcePath = original,
                DestinationPath = real,
                Mode = MigrationMode.LinkOnly,
                LinkKind = LinkKind.Junction,
                Kind = MigrationJobKind.Reconnect,
                Label = "手动接回",
            };

            var result = await new MigrationEngine().ExecuteAsync(request, relay, _cts.Token);

            var lines = new List<string> { result.Summary };
            if (result.Failure is not null) lines.Add($"{result.Failure.Operation}：{result.Failure.Message}");
            lines.AddRange(result.Notes);

            ShowResult(result.Succeeded ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
                result.OutcomeDisplay, string.Join(Environment.NewLine, lines));
        }
        finally
        {
            SetBusy(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    // ==================================================================
    private void SetBusy(bool busy, string? phase = null)
    {
        ProgressPanel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        FindButton.IsEnabled = !busy;
        CancelFindButton.IsEnabled = busy;
        ManualLinkButton.IsEnabled = !busy;
        ScanDriveBox.IsEnabled = !busy;
        ScanRootBox.IsEnabled = !busy;
        ReconnectButton.IsEnabled = !busy && CandidateList.SelectedItems.Cast<ScanItem>()
            .Any(i => !i.OriginAlreadyLinked && !i.OriginOccupied);

        if (busy)
        {
            ProgressBarControl.IsIndeterminate = true;
            if (phase is not null) ProgressPhaseText.Text = phase;
        }
    }

    private void ShowResult(InfoBarSeverity severity, string title, string message)
    {
        ResultBar.Severity = severity;
        ResultBar.Title = title;
        ResultBar.Message = string.Empty;
        ResultBar.IsOpen = true;

        ResultNotesText.Text = message;
        ResultNotesScroller.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
    }
}
