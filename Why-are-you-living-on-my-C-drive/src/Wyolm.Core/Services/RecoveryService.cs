using Wyolm.Core.Interop;
using Wyolm.Core.Models;

namespace Wyolm.Core.Services;

/// <summary>恢复逻辑对某一个中断作业采取的动作。</summary>
public enum RecoveryAction
{
    /// <summary>数据已经安全落到目标盘，把备份删完即可。</summary>
    FinishCommit,

    /// <summary>原地回滚：把备份改回原名，删掉目标副本。</summary>
    RollBack,

    /// <summary>作业尚未动过源目录，只清理目标上的半成品。</summary>
    CleanUpOnly,

    /// <summary>状态不明确或涉及合并目录，不自动处理。</summary>
    NeedsManualReview,
}

/// <summary>一条恢复记录。</summary>
public sealed class RecoveryItem
{
    public required string JobId { get; init; }
    public required string SourcePath { get; init; }
    public required string DestinationPath { get; init; }
    public MigrationPhase InterruptedAt { get; init; }
    public RecoveryAction Action { get; init; }
    public required string Detail { get; init; }
    public bool Succeeded { get; init; }
    public DateTimeOffset CompletedUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>一次启动恢复的汇总。</summary>
public sealed class RecoveryReport
{
    public required IReadOnlyList<RecoveryItem> Items { get; init; }

    public bool HasWork => Items.Count > 0;
    public int NeedsManual => Items.Count(i => i.Action == RecoveryAction.NeedsManualReview || !i.Succeeded);

    public string Summary => Items.Count == 0
        ? "没有发现中断的作业。"
        : $"发现并处理了 {Items.Count} 个中断的作业" +
          (NeedsManual > 0 ? $"，其中 {NeedsManual} 个需要人工确认。" : "，全部处理完毕。");
}

/// <summary>
/// 崩溃恢复。
/// <para>应用每次启动时跑一次。它读取所有没有正常结束的事务日志，
/// 结合磁盘上的实际状态判断"当时做到哪一步了"，然后二选一：
/// 要么把还没落地的改动清理干净（回滚），要么把已经落地的改动收尾完成。</para>
/// </summary>
public static class RecoveryService
{
    /// <summary>扫描并处理所有中断的作业。</summary>
    public static RecoveryReport Recover(CancellationToken ct = default)
    {
        var entries = MigrationJournal.LoadUnfinished();
        if (entries.Count == 0) return new RecoveryReport { Items = [] };

        var items = new List<RecoveryItem>();

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();

            using var log = new MigrationLog("recovery-" + entry.JobId);
            log.Info($"恢复作业 {entry.JobId}，日志记录的中断阶段：{entry.Phase}");

            RecoveryItem item;
            try
            {
                item = RecoverOne(entry, log);
            }
            catch (Exception ex)
            {
                log.Error("恢复过程中异常：" + ex);
                item = new RecoveryItem
                {
                    JobId = entry.JobId,
                    SourcePath = entry.SourcePath,
                    DestinationPath = entry.DestinationPath,
                    InterruptedAt = entry.Phase,
                    Action = RecoveryAction.NeedsManualReview,
                    Detail = $"恢复时发生异常：{ex.Message}",
                    Succeeded = false,
                };
            }

            items.Add(item);

            if (item.Succeeded)
                MigrationJournal.DeleteJournal(entry.JobId);

            log.Info($"恢复结果：{item.Action} / {(item.Succeeded ? "成功" : "未完成")} —— {item.Detail}");
        }

        return new RecoveryReport { Items = items };
    }

    private static RecoveryItem RecoverOne(JournalEntry entry, MigrationLog log)
    {
        var source = PathGuard.Normalize(entry.SourcePath);
        var destination = PathGuard.Normalize(entry.DestinationPath);
        var backup = entry.BackupPath is null ? null : PathGuard.Normalize(entry.BackupPath);

        bool sourceExists = Directory.Exists(source);
        var sourceKind = ReparsePoint.GetLinkKind(source);
        bool sourceIsRealDir = sourceExists && sourceKind == LinkKind.None;
        bool sourceIsLink = sourceKind != LinkKind.None;
        bool destinationExists = Directory.Exists(destination);
        bool backupExists = backup is not null && Directory.Exists(backup);

        log.Info($"磁盘现状：源={(sourceIsLink ? "链接" : sourceIsRealDir ? "真实目录" : "不存在")}，" +
                 $"目标={(destinationExists ? "存在" : "不存在")}，备份={(backupExists ? backup : "不存在")}");

        // ---------- 情况 0：链接模式（不搬运任何数据，回滚 = 拆掉链接） ----------
        if (entry.Mode == MigrationMode.LinkOnly)
        {
            if (sourceIsLink)
            {
                var target = ReparsePoint.ReadLinkTarget(source);
                bool correct = target is not null && destinationExists &&
                    string.Equals(target.TrimEnd('\\'), destination.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

                if (correct)
                {
                    return new RecoveryItem
                    {
                        JobId = entry.JobId,
                        SourcePath = source,
                        DestinationPath = destination,
                        InterruptedAt = entry.Phase,
                        Action = RecoveryAction.FinishCommit,
                        Detail = "链接已经建好且指向正确，无需处理。",
                        Succeeded = true,
                    };
                }

                bool removed = LinkService.TryDeleteLink(source);
                return new RecoveryItem
                {
                    JobId = entry.JobId,
                    SourcePath = source,
                    DestinationPath = destination,
                    InterruptedAt = entry.Phase,
                    Action = RecoveryAction.RollBack,
                    Detail = removed
                        ? "链接未完全建好（或指向错误），已拆除，原位置恢复为空。目标目录中的真实数据未被触碰。"
                        : $"未能拆除这个有问题的链接，请手动删除：{source}。目标目录中的数据未被触碰。",
                    Succeeded = removed,
                };
            }

            return new RecoveryItem
            {
                JobId = entry.JobId,
                SourcePath = source,
                DestinationPath = destination,
                InterruptedAt = entry.Phase,
                Action = RecoveryAction.CleanUpOnly,
                Detail = "链接模式作业中断，原位置没有留下链接，目标目录中的数据完整无损，无需处理。",
                Succeeded = true,
            };
        }

        // ---------- 情况 1：还没动过源目录（前置检查 / 复制 / 校验阶段中断） ----------
        if (entry.Phase <= MigrationPhase.Verifying)
        {
            if (sourceIsLink)
            {
                // 这种组合不该出现，保守处理。
                return Manual(entry, "源目录已经是链接，但日志显示还停留在复制阶段。",
                    "请人工确认链接指向是否正确。");
            }

            if (!destinationExists)
            {
                return new RecoveryItem
                {
                    JobId = entry.JobId,
                    SourcePath = source,
                    DestinationPath = destination,
                    InterruptedAt = entry.Phase,
                    Action = RecoveryAction.CleanUpOnly,
                    Detail = "源目录未受影响，目标位置也没留下东西，无需处理。",
                    Succeeded = true,
                };
            }

            if (entry.DestinationExistedBefore)
            {
                return Manual(entry,
                    "作业在复制阶段中断，目标目录在作业前就已有内容（合并模式），无法安全地自动清理。",
                    $"源目录未做任何改动。请手动检查并清理目标目录中多出来的内容：{destination}");
            }

            bool deleted = MigrationEngine.DeleteDirectorySafely(destination, log);
            return new RecoveryItem
            {
                JobId = entry.JobId,
                SourcePath = source,
                DestinationPath = destination,
                InterruptedAt = entry.Phase,
                Action = RecoveryAction.CleanUpOnly,
                Detail = deleted
                    ? "作业在复制/校验阶段中断，已删除目标上的半成品副本，源目录完好无损。"
                    : $"源目录完好无损，但半成品副本没能删干净，请手动清理：{destination}",
                Succeeded = deleted,
            };
        }

        // ---------- 情况 2：源目录已被改名（腾位 / 建链阶段中断） ----------
        if (entry.Phase is MigrationPhase.RenamingSource or MigrationPhase.Linking)
        {
            // 2a. 链接已经建好了：数据已在目标盘，收尾即可。
            if (sourceIsLink)
            {
                var target = ReparsePoint.ReadLinkTarget(source);
                bool pointsCorrectly = target is not null &&
                    string.Equals(target.TrimEnd('\\'), destination.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

                if (!destinationExists)
                {
                    // 目标没了但链接还在 —— 极其危险，别动任何东西。
                    return Manual(entry, "源位置是链接，但目标目录不存在了。",
                        $"请不要删除任何文件，先人工检查：{source} -> {target}");
                }

                if (!pointsCorrectly)
                {
                    return Manual(entry, $"源位置的链接指向了意料之外的地方：{target}",
                        $"期望指向 {destination}。请人工确认后处理。");
                }

                return FinishCommit(entry, source, destination, backup, backupExists, log);
            }

            // 2b. 链接没建好，但备份还在：把源目录改回去。
            if (backupExists && backup is not null)
            {
                try
                {
                    Directory.Move(backup, source);
                    log.Info($"已将备份改回原位置：{backup} -> {source}");
                }
                catch (Exception ex)
                {
                    return Manual(entry, $"把备份改回原位置失败：{ex.Message}",
                        $"原数据仍完整保留在：{backup}。请手动把它改名为：{source}");
                }

                bool cleaned = CleanDestinationIfSafe(entry, destination, destinationExists, log);

                return new RecoveryItem
                {
                    JobId = entry.JobId,
                    SourcePath = source,
                    DestinationPath = destination,
                    InterruptedAt = entry.Phase,
                    Action = RecoveryAction.RollBack,
                    Detail = cleaned
                        ? "作业在腾位/建链阶段中断，已把原目录恢复原位并清理目标副本，数据完好。"
                        : $"原目录已恢复原位，但目标副本未能清理干净，请手动检查：{destination}",
                    Succeeded = cleaned,
                };
            }

            // 2c. 既没有备份，也没有链接：改名这一步其实没执行（可能日志刚写完就崩了）。
            if (sourceIsRealDir)
            {
                bool cleaned = CleanDestinationIfSafe(entry, destination, destinationExists, log);
                return new RecoveryItem
                {
                    JobId = entry.JobId,
                    SourcePath = source,
                    DestinationPath = destination,
                    InterruptedAt = entry.Phase,
                    Action = RecoveryAction.RollBack,
                    Detail = cleaned
                        ? "源目录实际未被改动，已清理目标副本，数据完好。"
                        : $"源目录完好；目标副本未能清理干净，请手动检查：{destination}",
                    Succeeded = cleaned,
                };
            }

            return Manual(entry, "磁盘状态与日志记录不符：源位置不存在，也没有找到备份目录。",
                "请人工确认数据是否还在目标位置。");
        }

        // ---------- 情况 3：已过回滚边界（收尾 / 完成阶段中断） ----------
        // 数据已经在目标盘并且链接已验证可用，唯一要做的就是删掉残留的备份。
        if (sourceIsLink && destinationExists)
            return FinishCommit(entry, source, destination, backup, backupExists, log);

        if (sourceIsRealDir)
        {
            bool cleaned = CleanDestinationIfSafe(entry, destination, destinationExists, log);
            return new RecoveryItem
            {
                JobId = entry.JobId,
                SourcePath = source,
                DestinationPath = destination,
                InterruptedAt = entry.Phase,
                Action = RecoveryAction.RollBack,
                Detail = cleaned
                    ? "作业实际未越过回滚边界，已回到原始状态。"
                    : $"源目录完好；目标副本未能清理干净，请手动检查：{destination}",
                Succeeded = cleaned,
            };
        }

        return Manual(entry, "无法判断当前状态。", "请人工检查源位置、目标位置和备份目录。");
    }

    /// <summary>收尾：删掉残留的备份目录。</summary>
    private static RecoveryItem FinishCommit(
        JournalEntry entry, string source, string destination, string? backup, bool backupExists, MigrationLog log)
    {
        if (!backupExists || backup is null)
        {
            return new RecoveryItem
            {
                JobId = entry.JobId,
                SourcePath = source,
                DestinationPath = destination,
                InterruptedAt = entry.Phase,
                Action = RecoveryAction.FinishCommit,
                Detail = "链接与目标副本都正常，没有残留的旧数据，无需处理。",
                Succeeded = true,
            };
        }

        bool deleted = MigrationEngine.DeleteDirectorySafely(backup, log);
        return new RecoveryItem
        {
            JobId = entry.JobId,
            SourcePath = source,
            DestinationPath = destination,
            InterruptedAt = entry.Phase,
            Action = RecoveryAction.FinishCommit,
            Detail = deleted
                ? "链接与目标副本都正常，已删除中断时残留的原盘旧数据。"
                : $"链接与目标副本都正常，但残留的旧数据没能删干净，请手动清理：{backup}",
            Succeeded = deleted,
        };
    }

    /// <summary>
    /// 只在确认目标目录是本次作业新建的前提下才删它；
    /// 合并模式下绝不自动删除，避免把用户原有的文件一起清掉。
    /// </summary>
    private static bool CleanDestinationIfSafe(
        JournalEntry entry, string destination, bool destinationExists, MigrationLog log)
    {
        if (!destinationExists) return true;

        if (entry.DestinationExistedBefore)
        {
            log.Warn("目标目录在作业前就已有内容（合并模式），出于安全不做自动清理。");
            return false;
        }

        return MigrationEngine.DeleteDirectorySafely(destination, log);
    }

    private static RecoveryItem Manual(JournalEntry entry, string reason, string guidance) => new()
    {
        JobId = entry.JobId,
        SourcePath = entry.SourcePath,
        DestinationPath = entry.DestinationPath,
        InterruptedAt = entry.Phase,
        Action = RecoveryAction.NeedsManualReview,
        Detail = $"{reason} {guidance}",
        Succeeded = false,
    };
}
