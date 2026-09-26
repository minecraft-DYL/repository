using Wyolm.Core.Models;

namespace Wyolm.App.Services;

/// <summary>历史列表里的一行（已格式化成可直接显示的文本）。</summary>
public sealed class HistoryRow
{
    public required string Time { get; init; }
    public required string Mode { get; init; }
    public required string Outcome { get; init; }
    public required string Source { get; init; }
    public required string Destination { get; init; }
    public required string Size { get; init; }
    public required string Detail { get; init; }
    public bool IsProblem { get; init; }

    public static HistoryRow From(MigrationRecord r)
    {
        var mode = r.Mode switch
        {
            MigrationMode.LinkOnly => "只建链接",
            MigrationMode.CopyOnly => "只复制",
            _ => "搬家 + 链接",
        };

        var outcome = r.Outcome switch
        {
            MigrationOutcome.Success => "成功",
            MigrationOutcome.Cancelled => "已取消",
            MigrationOutcome.Rejected => "被拒绝",
            MigrationOutcome.FailedRolledBack => "失败·已回滚",
            MigrationOutcome.FailedRollbackIncomplete => "失败·回滚未完成",
            MigrationOutcome.DryRunPassed => "试运行通过",
            _ => r.Outcome.ToString(),
        };

        var problem = r.Outcome is MigrationOutcome.FailedRollbackIncomplete;

        var detailParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(r.Label)) detailParts.Add(r.Label!);
        detailParts.Add($"链接类型：{(r.LinkKind == LinkKind.Junction ? "目录联接" : "符号链接")}");
        if (r.FilesMoved > 0) detailParts.Add($"{r.FilesMoved:N0} 个文件");
        if (r.ElapsedSeconds > 0) detailParts.Add($"耗时 {r.ElapsedSeconds:0.#} 秒");
        if (!string.IsNullOrWhiteSpace(r.FailureMessage)) detailParts.Add("失败：" + r.FailureMessage);

        return new HistoryRow
        {
            Time = r.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
            Mode = mode,
            Outcome = outcome,
            Source = r.SourcePath,
            Destination = r.ActualDestinationPath ?? r.DestinationPath,
            Size = DriveInfoModel.FormatBytes(r.BytesMoved),
            Detail = string.Join("　·　", detailParts),
            IsProblem = problem,
        };
    }
}
