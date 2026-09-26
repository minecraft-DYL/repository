using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Wyolm.App.Services;
using Wyolm.Core.Models;
using Wyolm.Core.Services;

namespace Wyolm.App.Views;

/// <summary>扫描某个盘 / 目录，挑出体积大户，一键搬到别的盘并在原位留下链接。</summary>
public sealed partial class ScanPage : Page
{
    private readonly List<DriveInfoModel> _drives = [];
    private CancellationTokenSource? _cts;

    public ScanPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_drives.Count == 0) RefreshDrives();
    }

    /// <summary>重新枚举盘符。所有页面共用同一份缓存，避免反复枚举。</summary>
    private void RefreshDrives()
    {
        _drives.Clear();
        _drives.AddRange(DriveService.GetDrives(includeNonReady: false).Where(d => d.IsReady));
        _drives.AddRange(DriveService.GetDrives(includeNonReady: false).Where(d => !d.IsReady));

        SourceDriveBox.ItemsSource = _drives;
        TargetDriveBox.ItemsSource = _drives;

        var system = _drives.FirstOrDefault(d => d.IsSystem) ?? _drives.FirstOrDefault();
        if (system is not null)
        {
            SourceDriveBox.SelectedItem = system;
            SourcePathBox.Text = system.IsSystem
                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                : system.RootPath;
        }

        // 目标盘默认选第一个非系统盘，省得用户每次都要点。
        var target = _drives.FirstOrDefault(d => d.IsReady && !d.IsSystem) ?? _drives.FirstOrDefault(d => d.IsReady);
        if (target is not null) TargetDriveBox.SelectedItem = target;
    }

    private void SourceDriveBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SourceDriveBox.SelectedItem is not DriveInfoModel drive || !drive.IsReady) return;

        // 选 C 盘时默认落到用户目录 —— 那里才是真正占地的地方。
        SourcePathBox.Text = drive.IsSystem
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : drive.RootPath;
    }

    private void TargetDriveBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateDestinationPreview();

    private void UpdateDestinationPreview()
    {
        // 这个函数由 SelectionChanged / TextChanged 触发，而 XAML 加载期间
        // 排在后面的控件可能还没构造出来。控件没齐就先不更新，等加载完自然会再调一次。
        if (DestinationPreviewText is null || TargetDriveBox is null || TargetSubFolderBox is null) return;

        if (TargetDriveBox.SelectedItem is not DriveInfoModel drive || !drive.IsReady)
        {
            DestinationPreviewText.Text = "未选择目标盘";
            return;
        }

        var sub = TargetSubFolderBox.Text?.Trim() ?? string.Empty;
        var sample = ItemList is not null && ItemList.SelectedItems.Count > 0
            ? ((ScanItem)ItemList.SelectedItems[0]).Name
            : "文件夹名";

        DestinationPreviewText.Text = "→ " + UiHelpers.BuildDestination(drive.RootPath, sub, sample);
    }

    private async void BrowseSource_Click(object sender, RoutedEventArgs e)
    {
        var picked = await UiHelpers.PickFolderAsync(SourcePathBox.Text);
        if (!string.IsNullOrWhiteSpace(picked)) SourcePathBox.Text = picked;
    }

    // ==================================================================
    //  扫描
    // ==================================================================
    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        var root = SourcePathBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(root))
        {
            ShowResult(InfoBarSeverity.Warning, "还没有选择要扫描的目录", "请先指定一个盘符或目录。");
            return;
        }

        if (!Directory.Exists(root))
        {
            ShowResult(InfoBarSeverity.Error, "目录不存在", root);
            return;
        }

        _cts = new CancellationTokenSource();
        SetBusy(true, "正在扫描…");
        ItemList.ItemsSource = null;
        ResultBar.IsOpen = false;

        try
        {
            var relay = new ProgressRelay<ScanProgress>(DispatcherQueue, p =>
            {
                ScanStatusText.Text = p.Current is null
                    ? $"正在统计：{p.Done}/{p.Total}"
                    : $"正在统计（{p.Done}/{p.Total}）：{p.Current}";
            });

            var result = await DirectoryScanner.ScanChildrenAsync(root, includeHidden: false, relay, _cts.Token);

            ItemList.ItemsSource = result.Items;

            ScanStatusText.Text = result.Cancelled
                ? $"已取消，已统计 {result.Items.Count} 个文件夹。"
                : $"共 {result.Items.Count} 个文件夹，合计 {UiHelpers.FormatBytes(result.TotalBytes)}，" +
                  $"{result.TotalFiles:N0} 个文件，耗时 {UiHelpers.FormatDuration(result.Elapsed)}。";

            if (result.SkippedBecauseInaccessible > 0)
            {
                ShowResult(InfoBarSeverity.Warning, "扫描完成（有部分内容无法读取）",
                    $"有 {result.SkippedBecauseInaccessible} 个文件夹含有无权限访问或正被占用的内容，" +
                    "它们的体积统计会偏小。搬家时的校验也可能受影响。");
            }
        }
        catch (Exception ex)
        {
            ShowResult(InfoBarSeverity.Error, "扫描失败", ex.Message);
        }
        finally
        {
            SetBusy(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void CancelScan_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        ScanStatusText.Text = "正在取消…";
    }

    private void ItemList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = ItemList.SelectedItems.Cast<ScanItem>().ToList();
        var total = selected.Sum(i => i.SizeBytes);
        var links = selected.Count(i => i.IsLink);

        SelectionText.Text = selected.Count == 0
            ? "尚未选中任何文件夹"
            : links > 0
                ? $"已选 {selected.Count} 项，合计 {UiHelpers.FormatBytes(total)}（其中 {links} 项已经是链接，会被跳过）"
                : $"已选 {selected.Count} 项，合计 {UiHelpers.FormatBytes(total)}";

        MigrateButton.IsEnabled = selected.Any(i => !i.IsLink);
        UpdateDestinationPreview();
    }

    // ==================================================================
    //  搬家
    // ==================================================================
    private async void Migrate_Click(object sender, RoutedEventArgs e)
    {
        var selected = ItemList.SelectedItems.Cast<ScanItem>().Where(i => !i.IsLink).ToList();
        if (selected.Count == 0) return;

        if (TargetDriveBox.SelectedItem is not DriveInfoModel target || !target.IsReady)
        {
            ShowResult(InfoBarSeverity.Warning, "还没有选择目标盘", "请先指定要搬到哪个盘。");
            return;
        }

        var linkKind = LinkKindBox.SelectedIndex == 1 ? LinkKind.SymbolicLink : LinkKind.Junction;
        var verifyLevel = VerifyLevelBox.SelectedIndex switch
        {
            1 => VerifyLevel.ContentHash,
            2 => VerifyLevel.Structure,
            _ => VerifyLevel.SizeAndTimestamp,
        };

        var sub = TargetSubFolderBox.Text?.Trim() ?? string.Empty;
        var dryRun = DryRunSwitch.IsOn;
        var keepBackup = KeepBackupSwitch.IsOn;
        var totalBytes = selected.Sum(i => i.SizeBytes);

        // 真实执行前再确认一次，毕竟写的是用户的真实文件。
        if (!dryRun)
        {
            var confirm = new ContentDialog
            {
                Title = "确认开始搬家？",
                Content =
                    $"将处理 {selected.Count} 个文件夹，合计约 {UiHelpers.FormatBytes(totalBytes)}。\n\n" +
                    $"目标位置：{target.RootPath}{(string.IsNullOrWhiteSpace(sub) ? "" : sub + "\\")}…\n\n" +
                    "流程是：复制 → 校验 → 原位置建链接 → 删除原盘旧数据。" +
                    "任何一步失败都会自动回滚，原文件夹保持可用。\n\n" +
                    "期间请尽量不要使用这些文件夹里的程序，否则可能被迫中断。",
                PrimaryButtonText = "开始",
                CloseButtonText = "再想想",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
            };

            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        }

        _cts = new CancellationTokenSource();
        SetBusy(true, dryRun ? "正在试运行…" : "正在搬家…");
        ResultBar.IsOpen = false;

        var results = new List<MigrationResult>();

        try
        {
            for (int i = 0; i < selected.Count; i++)
            {
                var item = selected[i];
                var destination = UiHelpers.BuildDestination(target.RootPath, sub, item.Name);

                ProgressPhaseText.Text = $"[{i + 1}/{selected.Count}] {item.Name}";
                ProgressDetailText.Text = "准备中…";
                ProgressBarControl.IsIndeterminate = false;
                ProgressBarControl.Value = 0;

                var relay = new ProgressRelay<MigrationProgress>(DispatcherQueue, p =>
                {
                    ProgressPhaseText.Text = $"[{i + 1}/{selected.Count}] {item.Name} · {p.PhaseDisplay}";
                    ProgressDetailText.Text = BuildProgressDetail(p);
                    ProgressBarControl.IsIndeterminate = p.TotalBytes == 0 && p.Phase == MigrationPhase.Copying;
                    ProgressBarControl.Value = p.VerifyPercent ?? p.Percent;
                });

                var request = new MigrationRequest
                {
                    SourcePath = item.FullPath,
                    DestinationPath = destination,
                    Mode = MigrationMode.MoveAndLink,
                    LinkKind = linkKind,
                    VerifyLevel = verifyLevel,
                    Kind = MigrationJobKind.Relocate,
                    DryRun = dryRun,
                    KeepBackupAfterSuccess = keepBackup,
                    Label = $"扫描搬家：{item.Name}",
                };

                var result = await new MigrationEngine().ExecuteAsync(request, relay, _cts.Token);
                results.Add(result);

                // 前置检查没过就没必要继续处理后面的了：大概率是同一类问题。
                if (result.Outcome == MigrationOutcome.Rejected) break;
                if (_cts.IsCancellationRequested) break;
            }

            ShowAggregateResult(results, dryRun);
        }
        catch (Exception ex)
        {
            ShowResult(InfoBarSeverity.Error, "搬家过程出错", ex.Message);
        }
        finally
        {
            SetBusy(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private static string BuildProgressDetail(MigrationProgress p)
    {
        var parts = new List<string>();

        if (p.Phase == MigrationPhase.Verifying && p.VerifyPercent is double vp)
            parts.Add($"校验 {vp:0.#}%");
        else if (p.TotalBytes > 0)
            parts.Add($"{UiHelpers.FormatBytes(p.ProcessedBytes)} / {UiHelpers.FormatBytes(p.TotalBytes)}");
        else if (p.TotalFiles > 0)
            parts.Add($"{p.ProcessedFiles:N0} / {p.TotalFiles:N0} 个文件");

        if (!string.IsNullOrWhiteSpace(p.CurrentFile))
            parts.Add(p.CurrentFile.Length > 90 ? "…" + p.CurrentFile[^90..] : p.CurrentFile);

        return string.Join("　·　", parts);
    }

    private void ShowAggregateResult(List<MigrationResult> results, bool dryRun)
    {
        if (results.Count == 0)
        {
            ShowResult(InfoBarSeverity.Informational, "没有执行任何操作", "没有可处理的文件夹。");
            return;
        }

        int ok = results.Count(r => r.Succeeded);
        int rolledBack = results.Count(r => r.Outcome == MigrationOutcome.FailedRolledBack);
        int needsHelp = results.Count(r => r.Outcome == MigrationOutcome.FailedRollbackIncomplete);
        int rejected = results.Count(r => r.Outcome == MigrationOutcome.Rejected);
        int cancelled = results.Count(r => r.Outcome == MigrationOutcome.Cancelled);

        var severity = needsHelp > 0 ? InfoBarSeverity.Error
            : rolledBack > 0 || rejected > 0 ? InfoBarSeverity.Warning
            : InfoBarSeverity.Success;

        var title = dryRun
            ? $"试运行完成：{results.Count} 个文件夹"
            : $"完成：成功 {ok} 个" +
              (rolledBack > 0 ? $"，失败已回滚 {rolledBack} 个" : "") +
              (needsHelp > 0 ? $"，需人工处理 {needsHelp} 个" : "") +
              (rejected > 0 ? $"，被拒绝 {rejected} 个" : "") +
              (cancelled > 0 ? $"，取消 {cancelled} 个" : "");

        var lines = new List<string>();
        foreach (var r in results)
        {
            lines.Add($"· {Path.GetFileName(r.SourcePath)}：{r.OutcomeDisplay}");
            if (r.Failure is not null) lines.Add($"    {r.Failure.Phase} / {r.Failure.Operation}：{r.Failure.Message}");
            foreach (var n in r.Notes) lines.Add("    " + n);
            if (r.LogPath is not null && !r.Succeeded) lines.Add($"    日志：{r.LogPath}");
        }

        ShowResult(severity, title, string.Join(Environment.NewLine, lines));
    }

    // ==================================================================
    //  界面状态
    // ==================================================================
    private void SetBusy(bool busy, string? phase = null)
    {
        ProgressPanel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        ScanButton.IsEnabled = !busy;
        CancelScanButton.IsEnabled = busy;
        MigrateButton.IsEnabled = !busy && ItemList.SelectedItems.Cast<ScanItem>().Any(i => !i.IsLink);
        SourceDriveBox.IsEnabled = !busy;
        SourcePathBox.IsEnabled = !busy;
        TargetDriveBox.IsEnabled = !busy;
        TargetSubFolderBox.IsEnabled = !busy;
        BrowseSourceButton.IsEnabled = !busy;
        LinkKindBox.IsEnabled = !busy;
        VerifyLevelBox.IsEnabled = !busy;

        if (busy)
        {
            ProgressBarControl.IsIndeterminate = false;
            ProgressBarControl.Value = 0;
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
