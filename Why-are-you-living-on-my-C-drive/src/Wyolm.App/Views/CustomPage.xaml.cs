using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Wyolm.App.Services;
using Wyolm.Core.Models;
using Wyolm.Core.Services;

namespace Wyolm.App.Views;

/// <summary>把任意文件夹搬到任意位置，或在任意位置建立链接。</summary>
public sealed partial class CustomPage : Page
{
    private CancellationTokenSource? _cts;

    /// <summary>
    /// 页面控件是否已经全部构造完毕。
    /// <para>必须要有这个标志：XAML 里 <c>ModeBox</c> 上的 SelectedIndex 是在
    /// <c>InitializeComponent()</c> 执行到一半时生效的，那一刻它会触发
    /// <c>SelectionChanged</c>，而此时排在它后面的 <c>LinkKindBox</c> 等控件还是 null。
    /// 早期版本因此抛 NullReferenceException，直接把整个进程带走（0xc000027b）。</para>
    /// </summary>
    private bool _ready;

    public CustomPage()
    {
        InitializeComponent();
        _ready = true;
        UpdateHint();
    }

    private MigrationMode CurrentMode => ModeBox.SelectedIndex switch
    {
        1 => MigrationMode.CopyOnly,
        2 => MigrationMode.LinkOnly,
        _ => MigrationMode.MoveAndLink,
    };

    private void ModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        UpdateHint();
    }

    private void UpdateHint()
    {
        if (!_ready) return;
        if (PathHintText is null) return;

        PathHintText.Text = CurrentMode switch
        {
            MigrationMode.LinkOnly =>
                "「只建链接」模式：目标位置 = 数据现在真实所在的地方（必须已存在）；源文件夹 = 链接应该出现的位置（允许不存在，或是一个空文件夹）。不会复制任何数据。",
            MigrationMode.CopyOnly =>
                "「只复制」模式：源文件夹会原样复制到目标位置，原文件夹保持不动。目标位置请填完整的最终路径（含文件夹名）。",
            _ =>
                "「搬家」模式：源文件夹会被复制到目标位置、校验、然后在源位置建成目录联接，最后删除原盘旧数据。目标位置请填完整的最终路径（含文件夹名）。",
        };

        var linkOnly = CurrentMode == MigrationMode.LinkOnly;
        if (LinkKindBox is not null) LinkKindBox.IsEnabled = !linkOnly;
        if (KeepBackupSwitch is not null) KeepBackupSwitch.IsEnabled = !linkOnly;
        if (MergeSwitch is not null) MergeSwitch.IsEnabled = !linkOnly;
    }

    private async void BrowseSource_Click(object sender, RoutedEventArgs e)
    {
        var picked = await UiHelpers.PickFolderAsync(SourceBox.Text);
        if (!string.IsNullOrWhiteSpace(picked)) SourceBox.Text = picked;
    }

    private async void BrowseDestination_Click(object sender, RoutedEventArgs e)
    {
        var picked = await UiHelpers.PickFolderAsync(DestinationBox.Text);
        if (!string.IsNullOrWhiteSpace(picked)) DestinationBox.Text = picked;
    }

    /// <summary>常见场景：源是 C:\Users\me\.nuget，目标是 D 盘 → 补全成 D:\.nuget。</summary>
    private void AutoFillDestination_Click(object sender, RoutedEventArgs e)
    {
        var source = SourceBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(source))
        {
            ShowResult(InfoBarSeverity.Warning, "还没填源文件夹", "先选一个要搬走的文件夹。");
            return;
        }

        var name = Path.GetFileName(PathGuard.Normalize(source));
        if (string.IsNullOrEmpty(name))
        {
            ShowResult(InfoBarSeverity.Warning, "源文件夹名无法识别", "请不要选择盘符根目录。");
            return;
        }

        var drive = DriveService.GetDrives().FirstOrDefault(d => d.IsReady && !d.IsSystem)
                    ?? DriveService.GetDrives().FirstOrDefault(d => d.IsReady);

        if (drive is null)
        {
            ShowResult(InfoBarSeverity.Warning, "没有可用的目标盘", "请手动填写目标位置。");
            return;
        }

        DestinationBox.Text = Path.Combine(drive.RootPath, name);
    }

    // ==================================================================
    private async void Check_Click(object sender, RoutedEventArgs e)
    {
        var request = BuildRequest();
        if (request is null) return;

        ChecksPanel.Visibility = Visibility.Visible;
        ChecksList.ItemsSource = null;
        ResultBar.IsOpen = false;

        var report = await Task.Run(() => PathGuard.Check(request));

        ChecksList.ItemsSource = report.Checks
            .Select(c => $"{(c.Passed ? "✅" : c.IsBlocking ? "⛔" : "⚠️")} {c.Name}：{c.Detail}")
            .ToList();

        // 顺便估一下数据量，让用户心里有数。
        if (report.CanProceed && request.Mode != MigrationMode.LinkOnly && Directory.Exists(request.SourcePath))
        {
            ProgressPanel.Visibility = Visibility.Visible;
            ProgressPhaseText.Text = "正在估算体积…";
            ProgressBarControl.IsIndeterminate = true;

            var size = await Task.Run(() => DirectorySizer.Measure(request.SourcePath, CancellationToken.None));

            ProgressPanel.Visibility = Visibility.Collapsed;
            ProgressBarControl.IsIndeterminate = false;

            var extra = size.InaccessibleCount > 0
                ? $"，另有 {size.InaccessibleCount} 个条目无法读取"
                : string.Empty;

            ShowResult(report.CanProceed ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
                report.CanProceed ? "检查通过，可以执行" : "检查未通过，执行会被拒绝",
                $"数据量：{UiHelpers.FormatBytes(size.SizeBytes)} / {size.FileCount:N0} 个文件" +
                $"（清点耗时 {UiHelpers.FormatDuration(size.Elapsed)}）{extra}");
        }
        else
        {
            ShowResult(report.CanProceed ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
                report.CanProceed ? "检查通过，可以执行" : "检查未通过，执行会被拒绝",
                report.CanProceed
                    ? "没有发现问题。"
                    : string.Join(Environment.NewLine, report.Blockers.Select(b => $"· {b.Name}：{b.Detail}")));
        }
    }

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        var request = BuildRequest();
        if (request is null) return;

        if (!request.DryRun)
        {
            var modeText = request.Mode switch
            {
                MigrationMode.LinkOnly => "建立链接",
                MigrationMode.CopyOnly => "复制到目标位置",
                _ => "搬家（复制 → 校验 → 建链接 → 删除原盘旧数据）",
            };

            var confirm = new ContentDialog
            {
                Title = "确认执行？",
                Content = $"操作：{modeText}\n\n源：{request.SourcePath}\n目标：{request.DestinationPath}\n\n" +
                          "如果中途出错，Wyolm 会自动回滚到操作前的状态。",
                PrimaryButtonText = "执行",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };

            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        }

        _cts = new CancellationTokenSource();
        SetBusy(true);
        ResultBar.IsOpen = false;

        try
        {
            var relay = new ProgressRelay<MigrationProgress>(DispatcherQueue, p =>
            {
                ProgressPhaseText.Text = p.PhaseDisplay;
                ProgressBarControl.IsIndeterminate = p.TotalBytes == 0 && p.Phase == MigrationPhase.Copying;
                ProgressBarControl.Value = p.VerifyPercent ?? p.Percent;

                var parts = new List<string>();
                if (p.TotalBytes > 0)
                    parts.Add($"{UiHelpers.FormatBytes(p.ProcessedBytes)} / {UiHelpers.FormatBytes(p.TotalBytes)}");
                else if (p.TotalFiles > 0)
                    parts.Add($"{p.ProcessedFiles:N0} / {p.TotalFiles:N0} 个文件");
                if (!string.IsNullOrWhiteSpace(p.Message)) parts.Add(p.Message!);
                if (!string.IsNullOrWhiteSpace(p.CurrentFile)) parts.Add(p.CurrentFile!);
                ProgressDetailText.Text = string.Join("　·　", parts);
            });

            var result = await new MigrationEngine().ExecuteAsync(request, relay, _cts.Token);

            var lines = new List<string> { result.Summary };
            if (result.Preflight is not null)
            {
                lines.Add(string.Empty);
                lines.Add("前置检查：");
                lines.AddRange(result.Preflight.Checks.Select(c =>
                    $"  {(c.Passed ? "✅" : c.IsBlocking ? "⛔" : "⚠️")} {c.Name}：{c.Detail}"));
            }

            if (result.Verify is not null)
            {
                lines.Add(string.Empty);
                lines.AddRange(TreeVerifier.Describe(result.Verify).Select(l => "  " + l));
            }

            if (result.Failure is not null)
            {
                lines.Add(string.Empty);
                lines.Add($"失败位置：{result.Failure.Phase} / {result.Failure.Operation}");
                lines.Add($"原因：{result.Failure.Message}");
                if (result.Failure.ErrorCode is int code) lines.Add($"错误码：{code}");
            }

            foreach (var n in result.Notes) lines.Add("  " + n);

            lines.Add(string.Empty);
            lines.Add($"回滚状态：{result.Rollback}");
            if (result.LogPath is not null) lines.Add($"详细日志：{result.LogPath}");

            var severity = result.Outcome switch
            {
                MigrationOutcome.Success => InfoBarSeverity.Success,
                MigrationOutcome.DryRunPassed => InfoBarSeverity.Informational,
                MigrationOutcome.FailedRollbackIncomplete => InfoBarSeverity.Error,
                _ => InfoBarSeverity.Warning,
            };

            ShowResult(severity, result.OutcomeDisplay, string.Join(Environment.NewLine, lines));
        }
        catch (Exception ex)
        {
            ShowResult(InfoBarSeverity.Error, "执行出错", ex.Message);
        }
        finally
        {
            SetBusy(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        ProgressDetailText.Text = "正在取消…（已复制的内容会被清理掉）";
    }

    // ==================================================================
    private MigrationRequest? BuildRequest()
    {
        var source = SourceBox.Text?.Trim();
        var destination = DestinationBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(destination))
        {
            ShowResult(InfoBarSeverity.Warning, "还没填完整", "请把源文件夹和目标位置都填上。");
            return null;
        }

        try
        {
            // 提前规整一次，用户填的相对路径也能用。
            source = PathGuard.Normalize(source);
            destination = PathGuard.Normalize(destination);
        }
        catch (Exception ex)
        {
            ShowResult(InfoBarSeverity.Error, "路径无法解析", ex.Message);
            return null;
        }

        return new MigrationRequest
        {
            SourcePath = source,
            DestinationPath = destination,
            Mode = CurrentMode,
            LinkKind = LinkKindBox.SelectedIndex == 1 ? LinkKind.SymbolicLink : LinkKind.Junction,
            VerifyLevel = VerifyLevelBox.SelectedIndex switch
            {
                1 => VerifyLevel.ContentHash,
                2 => VerifyLevel.Structure,
                _ => VerifyLevel.SizeAndTimestamp,
            },
            Kind = MigrationJobKind.Custom,
            DryRun = DryRunSwitch.IsOn,
            KeepBackupAfterSuccess = KeepBackupSwitch.IsOn,
            AllowMergeIntoExisting = MergeSwitch.IsOn,
            SkipLockCheck = SkipLockCheckSwitch.IsOn,
            Label = "自定义迁移",
        };
    }

    private void SetBusy(bool busy)
    {
        ProgressPanel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        RunButton.IsEnabled = !busy;
        CheckButton.IsEnabled = !busy;
        CancelButton.IsEnabled = busy;
        ModeBox.IsEnabled = !busy;
        SourceBox.IsEnabled = !busy;
        DestinationBox.IsEnabled = !busy;

        if (busy)
        {
            ProgressBarControl.IsIndeterminate = false;
            ProgressBarControl.Value = 0;
            ProgressPhaseText.Text = "准备中…";
            ProgressDetailText.Text = string.Empty;
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
