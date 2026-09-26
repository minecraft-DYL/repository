using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Wyolm.App.Services;
using Wyolm.Core.Models;
using Wyolm.Core.Services;

namespace Wyolm.App.Views;

/// <summary>历史记录 + 启动时的崩溃恢复报告。</summary>
public sealed partial class HistoryPage : Page
{
    public HistoryPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Reload();
    }

    private void Reload()
    {
        // 历史
        var records = HistoryStore.Load();
        HistoryList.ItemsSource = records.Select(HistoryRow.From).ToList();
        CountText.Text = records.Count == 0 ? "还没有任何记录" : $"共 {records.Count} 条记录";

        // 启动恢复报告
        var report = App.StartupRecovery;
        if (report is null || !report.HasWork)
        {
            RecoveryPanel.Visibility = Visibility.Collapsed;
            return;
        }

        RecoveryPanel.Visibility = Visibility.Visible;
        RecoverySummaryText.Text = report.Summary;

        RecoveryList.ItemsSource = report.Items.Select(i =>
            $"【{DescribeAction(i.Action)}】{i.SourcePath}\n" +
            $"    中断阶段：{DescribePhase(i.InterruptedAt)}\n" +
            $"    {i.Detail}").ToList();
    }

    private static string DescribeAction(RecoveryAction action) => action switch
    {
        RecoveryAction.FinishCommit => "收尾完成",
        RecoveryAction.RollBack => "已回滚",
        RecoveryAction.CleanUpOnly => "已清理",
        _ => "需人工确认",
    };

    private static string DescribePhase(MigrationPhase phase) => phase switch
    {
        MigrationPhase.Preflight => "前置检查",
        MigrationPhase.Copying => "复制",
        MigrationPhase.Verifying => "校验",
        MigrationPhase.RenamingSource => "腾出原位置",
        MigrationPhase.Linking => "建立链接",
        MigrationPhase.Committing => "清理旧数据",
        MigrationPhase.Completed => "已完成",
        _ => phase.ToString(),
    };

    private void Refresh_Click(object sender, RoutedEventArgs e) => Reload();

    private void OpenLogs_Click(object sender, RoutedEventArgs e) => OpenFolder(AppPaths.LogDirectory);

    private void OpenData_Click(object sender, RoutedEventArgs e) => OpenFolder(AppPaths.DataRoot);

    private static void OpenFolder(string path)
    {
        try
        {
            AppPaths.EnsureCreated();
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // 打不开就算了，路径已经显示在界面上。
        }
    }

    private async void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        var confirm = new ContentDialog
        {
            Title = "清空历史记录？",
            Content = "只会删除操作记录，不会影响任何文件，也不会删除日志。",
            PrimaryButtonText = "清空",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };

        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        HistoryStore.Clear();
        Reload();
    }
}
