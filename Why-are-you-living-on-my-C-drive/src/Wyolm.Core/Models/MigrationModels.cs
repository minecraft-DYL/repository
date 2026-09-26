namespace Wyolm.Core.Models;

/// <summary>发起一次迁移所需的全部输入。</summary>
public sealed class MigrationRequest
{
    /// <summary>要被处理的源目录（绝对路径）。对 MoveAndLink 来说就是"C 盘那个大文件夹"。</summary>
    public required string SourcePath { get; init; }

    /// <summary>
    /// 目标位置。
    /// <para>MoveAndLink / CopyOnly：数据最终要被放到哪里。</para>
    /// <para>LinkOnly：链接要指向的真实目录（此时 SourcePath 是链接应该出现的位置）。</para>
    /// </summary>
    public required string DestinationPath { get; init; }

    public required MigrationMode Mode { get; init; }

    /// <summary>创建哪种链接。默认目录联接（不需要管理员）。</summary>
    public LinkKind LinkKind { get; init; } = LinkKind.Junction;

    /// <summary>校验强度。</summary>
    public VerifyLevel VerifyLevel { get; init; } = VerifyLevel.SizeAndTimestamp;

    public MigrationJobKind Kind { get; init; } = MigrationJobKind.Custom;

    /// <summary>
    /// 是否真的执行。为 false 时引擎只做前置检查并返回报告，不碰任何文件。
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// 即便目标已存在且非空也继续（默认会在复制阶段合并、冲突时报错）。
    /// </summary>
    public bool AllowMergeIntoExisting { get; init; }

    /// <summary>
    /// 覆盖锁检测。默认会尝试探测是否有进程正在占用源目录里的文件。
    /// </summary>
    public bool SkipLockCheck { get; init; }

    /// <summary>保留源目录的只读/隐藏属性等（默认保留）。</summary>
    public bool PreserveAttributes { get; init; } = true;

    /// <summary>
    /// 成功后保留原目录的备份副本，不自动删除。
    /// <para>给谨慎的用户留一条后路：链接已经建好、数据已在目标盘，
    /// 但原盘那份暂时留着，等用户自己确认没问题后再手动清理。</para>
    /// </summary>
    public bool KeepBackupAfterSuccess { get; init; }

    /// <summary>可选的备注，会写进历史记录。</summary>
    public string? Label { get; init; }
}

/// <summary>前置检查的单项结果。</summary>
public sealed record PreflightCheck(string Name, bool Passed, string Detail, bool IsBlocking = true);

/// <summary>前置检查汇总。</summary>
public sealed class PreflightReport
{
    public required IReadOnlyList<PreflightCheck> Checks { get; init; }

    public bool CanProceed => Checks.All(c => c.Passed || !c.IsBlocking);

    public IEnumerable<PreflightCheck> Blockers => Checks.Where(c => !c.Passed && c.IsBlocking);

    public IEnumerable<PreflightCheck> Warnings => Checks.Where(c => !c.Passed && !c.IsBlocking);
}

/// <summary>迁移过程中向界面汇报的进度。</summary>
public sealed class MigrationProgress
{
    public required MigrationPhase Phase { get; init; }
    public string? Message { get; init; }
    public long TotalBytes { get; init; }
    public long ProcessedBytes { get; init; }
    public long TotalFiles { get; init; }
    public long ProcessedFiles { get; init; }
    public string? CurrentFile { get; init; }

    /// <summary>校验阶段的百分比（0-100），其他阶段为 null。</summary>
    public double? VerifyPercent { get; init; }

    public double Percent => TotalBytes > 0
        ? Math.Clamp((double)ProcessedBytes / TotalBytes * 100.0, 0, 100)
        : (TotalFiles > 0 ? Math.Clamp((double)ProcessedFiles / TotalFiles * 100.0, 0, 100) : 0);

    public string PhaseDisplay => Phase switch
    {
        MigrationPhase.NotStarted => "待开始",
        MigrationPhase.Preflight => "前置检查",
        MigrationPhase.Copying => "复制中",
        MigrationPhase.Verifying => "校验中",
        MigrationPhase.RenamingSource => "腾出原位置",
        MigrationPhase.Linking => "建立链接",
        MigrationPhase.Committing => "清理旧数据",
        MigrationPhase.Completed => "已完成",
        MigrationPhase.RolledBack => "已回滚",
        MigrationPhase.RollbackFailed => "回滚失败",
        _ => Phase.ToString(),
    };
}

/// <summary>违反的校验项。</summary>
public sealed record VerifyMismatch(string RelativePath, string Reason, long SourceValue, long DestinationValue);

/// <summary>校验报告。</summary>
public sealed class VerifyReport
{
    public required VerifyLevel Level { get; init; }
    public required IReadOnlyList<VerifyMismatch> Mismatches { get; init; }
    public long ComparedFiles { get; init; }
    public long ComparedBytes { get; init; }
    public long HashedFiles { get; init; }
    public TimeSpan Elapsed { get; init; }

    public bool Passed => Mismatches.Count == 0;

    public string Summary => Passed
        ? $"校验通过：{ComparedFiles} 个文件 / {DriveInfoModel.FormatBytes(ComparedBytes)}（{Level}）"
        : $"校验失败：发现 {Mismatches.Count} 处不一致（{Level}）";
}

/// <summary>失败详情。</summary>
public sealed record MigrationFailure(MigrationPhase Phase, string Operation, string Message, int? ErrorCode, string? Path);

/// <summary>一次迁移的完整结果。</summary>
public sealed class MigrationResult
{
    public required MigrationJobKind Kind { get; init; }
    public required MigrationMode Mode { get; init; }
    public required string SourcePath { get; init; }
    public required string DestinationPath { get; init; }
    public required MigrationOutcome Outcome { get; init; }
    public MigrationPhase FinalPhase { get; init; }
    public required RollbackState Rollback { get; init; }

    public PreflightReport? Preflight { get; init; }
    public VerifyReport? Verify { get; init; }
    public MigrationFailure? Failure { get; init; }

    public long BytesMoved { get; init; }
    public long FilesMoved { get; init; }
    public TimeSpan Elapsed { get; init; }

    /// <summary>回滚过程中产生的额外说明（例如备份目录没能删干净）。</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    /// <summary>事务日志文件路径，出问题时用户可以拿它来复盘。</summary>
    public string? LogPath { get; init; }

    /// <summary>落盘的目标路径（MoveAndLink 时可能因为重名而变成 "name (2)"）。</summary>
    public string? ActualDestinationPath { get; init; }

    public bool Succeeded => Outcome == MigrationOutcome.Success;

    public string OutcomeDisplay => Outcome switch
    {
        MigrationOutcome.Success => "成功",
        MigrationOutcome.Cancelled => "已取消",
        MigrationOutcome.Rejected => "前置检查未通过",
        MigrationOutcome.FailedRolledBack => "失败，已自动回滚",
        MigrationOutcome.FailedRollbackIncomplete => "失败，回滚未完成（需人工处理）",
        MigrationOutcome.DryRunPassed => "试运行通过（未做任何改动）",
        _ => Outcome.ToString(),
    };

    public string Summary
    {
        get
        {
            var head = $"{OutcomeDisplay}：{SourcePath} → {DestinationPath}";
            if (Outcome == MigrationOutcome.Success)
                return $"{head}（{DriveInfoModel.FormatBytes(BytesMoved)} / {FilesMoved} 个文件）";
            if (Failure is not null)
                return $"{head}｜{Failure.Phase} 阶段出错：{Failure.Message}";
            return head;
        }
    }
}
