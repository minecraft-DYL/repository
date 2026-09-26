using System.ComponentModel;
using Wyolm.Core.Interop;
using Wyolm.Core.Models;

namespace Wyolm.Core.Services;

/// <summary>
/// 迁移引擎 —— 本工具的核心状态机。
///
/// <para><b>搬家（MoveAndLink）的步骤与每一处失败会发生什么：</b></para>
/// <list type="number">
/// <item>前置检查：路径合法性、空间、占用。不通过就直接拒绝，一个字节都不动。</item>
/// <item>复制：源 → 目标。失败 → 删掉复制了一半的目标，源完好。 </item>
/// <item>校验：结构 / 大小 / 时间戳 /（可选）SHA-256。失败 → 同样只删目标副本，源完好。</item>
/// <item>腾位：源目录改名为同盘的临时备份目录。改名是原语，要么成功要么没发生。</item>
/// <item>建链：在原位置建目录联接。失败 → 把备份改回原名，删掉目标副本，完全还原。</item>
/// <item>确认：透过链接真实读一次文件，确认链接通着。</item>
/// <item>收尾：确认无误后才删除备份。到这一步数据已经躺在目标盘并且链接可用，
/// 所以即使这里断电，恢复逻辑也只会"把备份删完"，而不会去回滚一份已经生效的成果。</item>
/// </list>
///
/// <para><b>为什么第 7 步之后不再回滚：</b>因为此时目标副本已经过校验、链接已经验证可用。
/// 若此时备份目录被删了一半再回滚，只会把一份残缺的数据搬回原位置 —— 那才是真正的数据损失。
/// 所以回滚的边界就卡在"链接确认可用"这一条线上。</para>
/// </summary>
public sealed class MigrationEngine
{
    /// <summary>
    /// 仅供测试使用的故障注入点：复制完成之后、校验之前，拿目标目录路径调一次。
    /// <para>存在的理由很实际 —— "校验失败时能不能干净地回滚"是这个工具最核心的承诺之一，
    /// 而校验失败在真机上很难自然地制造出来。有了这个缝，测试可以精确地把副本改坏，
    /// 然后验证引擎确实删掉了副本、把源目录原封不动地留了下来。</para>
    /// </summary>
    internal static Action<string>? DestinationTamperHookForTests { get; set; }

    /// <summary>执行一次迁移。</summary>
    public Task<MigrationResult> ExecuteAsync(
        MigrationRequest request,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken ct = default)
        => Task.Run(() => Execute(request, progress, ct), ct);

    private MigrationResult Execute(
        MigrationRequest request,
        IProgress<MigrationProgress>? progress,
        CancellationToken ct)
    {
        using var journal = new MigrationJournal();
        using var log = new MigrationLog(journal.JobId);

        log.Info($"作业 {journal.JobId} 开始：模式={request.Mode}，类型={request.Kind}");
        log.Info($"源={request.SourcePath}");
        log.Info($"目标={request.DestinationPath}");
        log.Info($"链接类型={request.LinkKind}，校验级别={request.VerifyLevel}");

        try
        {
            return request.Mode switch
            {
                MigrationMode.LinkOnly => RunLinkOnly(request, journal, log, progress, ct),
                MigrationMode.CopyOnly => RunCopyOnly(request, journal, log, progress, ct),
                MigrationMode.MoveAndLink => RunMoveAndLink(request, journal, log, progress, ct),
                _ => throw new ArgumentOutOfRangeException(nameof(request), request.Mode, "不支持的迁移模式。"),
            };
        }
        catch (OperationCanceledException)
        {
            log.Warn("作业被取消。");
            var result = BuildResult(request, MigrationOutcome.Cancelled, MigrationPhase.NotStarted,
                RollbackState.NotNeeded, journal.JobId, log, description: "用户取消了操作。");
            Record(request, result, log.Path);
            return result;
        }
        catch (Exception ex)
        {
            log.Error($"未预期的异常：{ex}");
            var result = BuildResult(request, MigrationOutcome.FailedRollbackIncomplete, MigrationPhase.NotStarted,
                RollbackState.Failed, journal.JobId, log, failure: Describe(MigrationPhase.NotStarted, "未知操作", ex));
            Record(request, result, log.Path);
            return result;
        }
    }

    // ==================================================================
    //  只建链接
    // ==================================================================
    private MigrationResult RunLinkOnly(
        MigrationRequest request, MigrationJournal journal, MigrationLog log,
        IProgress<MigrationProgress>? progress, CancellationToken ct)
    {
        var source = PathGuard.Normalize(request.SourcePath);       // 链接应该出现的位置
        var destination = PathGuard.Normalize(request.DestinationPath); // 真实数据所在处

        progress?.Report(new MigrationProgress { Phase = MigrationPhase.Preflight, Message = "正在检查路径…" });

        var preflight = PathGuard.Check(request);
        foreach (var c in preflight.Checks)
            log.Info($"前置检查 [{(c.Passed ? "通过" : "未通过")}] {c.Name}：{c.Detail}");

        if (!preflight.CanProceed)
        {
            var reason = string.Join("；", preflight.Blockers.Select(b => $"{b.Name}：{b.Detail}"));
            log.Error("前置检查未通过，已放弃：" + reason);
            journal.Complete();

            var rejected = BuildResult(request, MigrationOutcome.Rejected, MigrationPhase.Preflight,
                RollbackState.NotNeeded, journal.JobId, log, preflight: preflight,
                failure: new MigrationFailure(MigrationPhase.Preflight, "前置检查", reason, null, source));
            Record(request, rejected, log.Path);
            return rejected;
        }

        if (request.DryRun)
        {
            journal.Complete();
            var dry = BuildResult(request, MigrationOutcome.DryRunPassed, MigrationPhase.Preflight,
                RollbackState.NotNeeded, journal.JobId, log, preflight: preflight);
            Record(request, dry, log.Path);
            return dry;
        }

        var entry = new JournalEntry
        {
            JobId = journal.JobId,
            Phase = MigrationPhase.Linking,
            Mode = request.Mode,
            SourcePath = source,
            DestinationPath = destination,
            // 链接模式不复制任何数据，所以 CopiedPath 留空 ——
            // 恢复逻辑绝不能把目标里的真实数据当成"半成品副本"删掉。
            DestinationExistedBefore = true,
        };
        journal.Write(entry);

        bool linkCreated = false;
        var notes = new List<string>();

        try
        {
            progress?.Report(new MigrationProgress
            {
                Phase = MigrationPhase.Linking,
                Message = $"正在建立{(request.LinkKind == LinkKind.Junction ? "目录联接" : "符号链接")}…",
            });

            // 原位置可能有一个空的占位目录，LinkService.Create 会清掉它。
            LinkService.Create(source, destination, request.LinkKind);
            linkCreated = true;
            log.Info($"链接已建立：{source} -> {destination}");

            if (!VerifyLink(source, destination, log, out var linkProblem))
                throw new IOException($"链接建立后自检失败：{linkProblem}");

            entry.LinkCreated = true;
            journal.Write(entry);

            // 补写来源标记，让以后在别的机器上也能扫出来。
            var marker = OriginMarkerStore.Read(destination);
            if (marker is null)
            {
                OriginMarkerStore.Write(destination, OriginMarkerStore.Create(source, destination, request.LinkKind,
                    note: "由链接模式创建（数据未经过本工具搬运）"));
                log.Info("已在目标目录写入来源标记。");
            }
            else
            {
                marker.CurrentPath = destination;
                marker.OriginalPath = source;
                OriginMarkerStore.Write(destination, marker);
                log.Info("已更新目标目录的来源标记。");
            }

            entry.Phase = MigrationPhase.Completed;
            entry.Finished = true;
            journal.Write(entry);
            journal.Complete();

            log.Info("作业成功完成。");
            var result = BuildResult(request, MigrationOutcome.Success, MigrationPhase.Completed,
                RollbackState.NotNeeded, journal.JobId, log, preflight: preflight, notes: notes,
                actualDestination: destination);
            Record(request, result, log.Path);
            return result;
        }
        catch (Exception ex)
        {
            log.Error($"建立链接失败：{ex.Message}");

            var rollbackState = RollbackState.NotNeeded;
            if (linkCreated)
            {
                progress?.Report(new MigrationProgress { Phase = MigrationPhase.Linking, Message = "正在拆除失败的链接…" });
                if (LinkService.TryDeleteLink(source))
                {
                    log.Info("已拆除失败的链接，原位置恢复为空。");
                    rollbackState = RollbackState.Succeeded;
                }
                else
                {
                    log.Error("链接拆除失败，需要人工检查：" + source);
                    notes.Add($"⚠ 未能拆除链接，请手动检查并删除：{source}");
                    rollbackState = RollbackState.Failed;
                }
            }

            journal.Complete();

            var outcome = rollbackState == RollbackState.Failed
                ? MigrationOutcome.FailedRollbackIncomplete
                : MigrationOutcome.FailedRolledBack;

            var result = BuildResult(request, outcome,
                rollbackState == RollbackState.Failed ? MigrationPhase.RollbackFailed : MigrationPhase.RolledBack,
                rollbackState, journal.JobId, log, preflight: preflight,
                failure: Describe(MigrationPhase.Linking, "建立链接", ex), notes: notes);
            Record(request, result, log.Path);
            return result;
        }
    }

    // ==================================================================
    //  只复制
    // ==================================================================
    private MigrationResult RunCopyOnly(
        MigrationRequest request, MigrationJournal journal, MigrationLog log,
        IProgress<MigrationProgress>? progress, CancellationToken ct)
    {
        var source = PathGuard.Normalize(request.SourcePath);
        var destination = PathGuard.Normalize(request.DestinationPath);
        var notes = new List<string>();

        progress?.Report(new MigrationProgress { Phase = MigrationPhase.Preflight, Message = "正在检查路径…" });

        var preflight = PathGuard.Check(request);
        foreach (var c in preflight.Checks)
            log.Info($"前置检查 [{(c.Passed ? "通过" : "未通过")}] {c.Name}：{c.Detail}");

        if (!preflight.CanProceed)
        {
            var reason = string.Join("；", preflight.Blockers.Select(b => $"{b.Name}：{b.Detail}"));
            log.Error("前置检查未通过，已放弃：" + reason);
            journal.Complete();
            var rejected = BuildResult(request, MigrationOutcome.Rejected, MigrationPhase.Preflight,
                RollbackState.NotNeeded, journal.JobId, log, preflight: preflight,
                failure: new MigrationFailure(MigrationPhase.Preflight, "前置检查", reason, null, source));
            Record(request, rejected, log.Path);
            return rejected;
        }

        if (request.DryRun)
        {
            journal.Complete();
            var dry = BuildResult(request, MigrationOutcome.DryRunPassed, MigrationPhase.Preflight,
                RollbackState.NotNeeded, journal.JobId, log, preflight: preflight);
            Record(request, dry, log.Path);
            return dry;
        }

        bool destinationExistedBefore = Directory.Exists(destination);
        var createdFiles = new List<string>();
        var entry = new JournalEntry
        {
            JobId = journal.JobId,
            Phase = MigrationPhase.Copying,
            Mode = request.Mode,
            SourcePath = source,
            DestinationPath = destination,
            CopiedPath = destination,
            DestinationExistedBefore = destinationExistedBefore,
        };
        journal.Write(entry);

        try
        {
            progress?.Report(new MigrationProgress { Phase = MigrationPhase.Copying, Message = "正在清点文件…" });
            var manifest = TreeScanner.Build(source, ct, progress is null ? null : new Progress<(long, long, string?)>(p =>
                progress.Report(new MigrationProgress
                {
                    Phase = MigrationPhase.Copying,
                    Message = "正在清点文件…",
                    TotalFiles = p.Item1,
                    ProcessedFiles = p.Item1,
                    TotalBytes = p.Item2,
                    ProcessedBytes = p.Item2,
                    CurrentFile = p.Item3,
                })));
            log.Info($"清单：{manifest.Files.Count} 个文件 / {manifest.Directories.Count} 个目录 / {DriveInfoModel.FormatBytes(manifest.TotalBytes)}");

            if (manifest.Inaccessible.Count > 0)
            {
                notes.Add($"有 {manifest.Inaccessible.Count} 个条目因权限或占用无法读取，已跳过。");
                foreach (var p in manifest.Inaccessible.Take(10)) log.Warn("跳过不可读路径：" + p);
            }

            progress?.Report(new MigrationProgress
            {
                Phase = MigrationPhase.Copying,
                Message = "正在复制…",
                TotalBytes = manifest.TotalBytes,
                TotalFiles = manifest.Files.Count,
            });

            FileTreeCopier.Copy(manifest, destination, request.PreserveAttributes,
                request.AllowMergeIntoExisting, ct,
                progress is null ? null : new Progress<CopyProgress>(p => progress.Report(new MigrationProgress
                {
                    Phase = MigrationPhase.Copying,
                    Message = "正在复制…",
                    TotalBytes = p.TotalBytes,
                    ProcessedBytes = p.CopiedBytes,
                    TotalFiles = p.TotalFiles,
                    ProcessedFiles = p.CopiedFiles,
                    CurrentFile = p.CurrentFile,
                })),
                log.Info,
                createdFiles);

            DestinationTamperHookForTests?.Invoke(destination);

            progress?.Report(new MigrationProgress { Phase = MigrationPhase.Verifying, Message = "正在校验副本…" });
            var verify = TreeVerifier.Verify(manifest, destination, request.VerifyLevel, ct,
                progress is null ? null : new Progress<VerifyProgress>(p => progress.Report(new MigrationProgress
                {
                    Phase = MigrationPhase.Verifying,
                    Message = "正在校验副本…",
                    VerifyPercent = p.Total > 0 ? (double)p.Checked / p.Total * 100 : 0,
                    CurrentFile = p.Current,
                })));

            foreach (var line in TreeVerifier.Describe(verify)) log.Info(line);

            if (!verify.Passed)
            {
                log.Error("校验未通过，开始回滚（源目录未做任何改动）。");
                var rb = DeleteCopiedData(destination, destinationExistedBefore, createdFiles, log, notes);

                journal.Complete();
                var failed = BuildResult(request, rb == RollbackState.Succeeded || rb == RollbackState.NotNeeded
                        ? MigrationOutcome.FailedRolledBack
                        : MigrationOutcome.FailedRollbackIncomplete,
                    MigrationPhase.RolledBack, rb, journal.JobId, log, preflight: preflight,
                    verify: verify, notes: notes,
                    failure: new MigrationFailure(MigrationPhase.Verifying, "副本校验",
                        verify.Summary, null, destination));
                Record(request, failed, log.Path);
                return failed;
            }

            // 写入来源标记，方便以后接回原位。
            OriginMarkerStore.Write(destination, OriginMarkerStore.Create(source, destination, request.LinkKind,
                manifest.TotalBytes, manifest.Files.Count, "由复制模式创建（原目录仍保留）"));
            log.Info("已在目标目录写入来源标记。");

            entry.Phase = MigrationPhase.Completed;
            entry.Finished = true;
            journal.Write(entry);
            journal.Complete();

            log.Info("作业成功完成。");
            var result = BuildResult(request, MigrationOutcome.Success, MigrationPhase.Completed,
                RollbackState.NotNeeded, journal.JobId, log, preflight: preflight, verify: verify,
                notes: notes, bytesMoved: manifest.TotalBytes, filesMoved: manifest.Files.Count,
                actualDestination: destination);
            Record(request, result, log.Path);
            return result;
        }
        catch (OperationCanceledException)
        {
            log.Warn("复制被取消，开始回滚。");
            var rb = DeleteCopiedData(destination, destinationExistedBefore, createdFiles, log, notes);
            journal.Complete();
            var cancelled = BuildResult(request, MigrationOutcome.Cancelled, MigrationPhase.RolledBack, rb,
                journal.JobId, log, preflight: preflight, notes: notes, description: "用户取消了复制。");
            Record(request, cancelled, log.Path);
            return cancelled;
        }
        catch (Exception ex)
        {
            log.Error($"复制 / 校验阶段失败：{ex.Message}");
            var rb = DeleteCopiedData(destination, destinationExistedBefore, createdFiles, log, notes);
            journal.Complete();

            var result = BuildResult(request, rb == RollbackState.Failed
                    ? MigrationOutcome.FailedRollbackIncomplete
                    : MigrationOutcome.FailedRolledBack,
                rb == RollbackState.Failed ? MigrationPhase.RollbackFailed : MigrationPhase.RolledBack,
                rb, journal.JobId, log, preflight: preflight, notes: notes,
                failure: Describe(MigrationPhase.Copying, "复制", ex));
            Record(request, result, log.Path);
            return result;
        }
    }

    // ==================================================================
    //  搬家 + 建链接
    // ==================================================================
    private MigrationResult RunMoveAndLink(
        MigrationRequest request, MigrationJournal journal, MigrationLog log,
        IProgress<MigrationProgress>? progress, CancellationToken ct)
    {
        var source = PathGuard.Normalize(request.SourcePath);
        var destination = PathGuard.Normalize(request.DestinationPath);
        var notes = new List<string>();

        var entry = new JournalEntry
        {
            JobId = journal.JobId,
            Phase = MigrationPhase.Preflight,
            Mode = request.Mode,
            SourcePath = source,
            DestinationPath = destination,
        };
        journal.Write(entry);

        // ---------- 1. 前置检查 ----------
        progress?.Report(new MigrationProgress { Phase = MigrationPhase.Preflight, Message = "正在检查路径…" });

        var preflight = PathGuard.Check(request);
        foreach (var c in preflight.Checks)
            log.Info($"前置检查 [{(c.Passed ? "通过" : "未通过")}] {c.Name}：{c.Detail}");

        if (!preflight.CanProceed)
        {
            var reason = string.Join("；", preflight.Blockers.Select(b => $"{b.Name}：{b.Detail}"));
            log.Error("前置检查未通过，已放弃：" + reason);
            journal.Complete();
            var rejected = BuildResult(request, MigrationOutcome.Rejected, MigrationPhase.Preflight,
                RollbackState.NotNeeded, journal.JobId, log, preflight: preflight,
                failure: new MigrationFailure(MigrationPhase.Preflight, "前置检查", reason, null, source));
            Record(request, rejected, log.Path);
            return rejected;
        }

        // ---------- 2. 占用探测 ----------
        if (!request.SkipLockCheck)
        {
            progress?.Report(new MigrationProgress { Phase = MigrationPhase.Preflight, Message = "正在检查文件占用…" });
            var locks = LockDetector.Inspect(source, 200, ct);
            log.Info("占用探测：" + locks.Summary);

            if (locks.HasBlockers)
            {
                var reason = locks.Summary + "。请先退出这些程序再重试。";
                log.Error("发现占用进程，已放弃：" + reason);
                journal.Complete();
                var blocked = BuildResult(request, MigrationOutcome.Rejected, MigrationPhase.Preflight,
                    RollbackState.NotNeeded, journal.JobId, log, preflight: preflight,
                    failure: new MigrationFailure(MigrationPhase.Preflight, "占用探测", reason, null, source));
                Record(request, blocked, log.Path);
                return blocked;
            }

            if (locks.LockedSamples.Count > 0)
            {
                notes.Add($"有 {locks.LockedSamples.Count} 个样本文件当前被占用，若后续失败会自动回滚。");
            }
        }

        if (request.DryRun)
        {
            journal.Complete();
            var dry = BuildResult(request, MigrationOutcome.DryRunPassed, MigrationPhase.Preflight,
                RollbackState.NotNeeded, journal.JobId, log, preflight: preflight);
            Record(request, dry, log.Path);
            return dry;
        }

        // ---------- 3. 清点 ----------
        progress?.Report(new MigrationProgress { Phase = MigrationPhase.Copying, Message = "正在清点文件…" });

        TreeManifest manifest;
        try
        {
            manifest = TreeScanner.Build(source, ct, progress is null ? null : new Progress<(long, long, string?)>(p =>
                progress.Report(new MigrationProgress
                {
                    Phase = MigrationPhase.Copying,
                    Message = "正在清点文件…",
                    TotalFiles = p.Item1,
                    ProcessedFiles = p.Item1,
                    TotalBytes = p.Item2,
                    ProcessedBytes = p.Item2,
                    CurrentFile = p.Item3,
                })));
        }
        catch (OperationCanceledException)
        {
            journal.Complete();
            return BuildResult(request, MigrationOutcome.Cancelled, MigrationPhase.NotStarted, RollbackState.NotNeeded,
                journal.JobId, log, preflight: preflight, description: "用户取消了操作。");
        }

        log.Info($"清单：{manifest.Files.Count} 个文件 / {manifest.Directories.Count} 个目录 / " +
                 $"{manifest.Links.Count} 个嵌套链接 / {DriveInfoModel.FormatBytes(manifest.TotalBytes)}");

        if (manifest.Inaccessible.Count > 0)
        {
            notes.Add($"有 {manifest.Inaccessible.Count} 个条目无法读取，已跳过；建议确认这些文件不重要后再继续。");
            foreach (var p in manifest.Inaccessible.Take(10)) log.Warn("跳过不可读路径：" + p);
        }

        bool destinationExistedBefore = Directory.Exists(destination);
        var createdFiles = new List<string>();

        // 备份目录：和源目录同盘同父目录，改名是瞬间完成的文件系统原语。
        var sourceParent = Path.GetDirectoryName(source)
                           ?? throw new IOException($"无法确定源目录的父目录：{source}");
        var backupPath = Path.Combine(sourceParent,
            $".{Path.GetFileName(source)}.wyolm-backup-{journal.JobId[^6..]}");

        bool sourceRenamed = false;
        bool linkCreated = false;

        try
        {
            // ---------- 4. 复制 ----------
            entry.Phase = MigrationPhase.Copying;
            entry.CopiedPath = destination;
            entry.BackupPath = backupPath;
            entry.DestinationExistedBefore = destinationExistedBefore;
            journal.Write(entry);

            progress?.Report(new MigrationProgress
            {
                Phase = MigrationPhase.Copying,
                Message = "正在复制…",
                TotalBytes = manifest.TotalBytes,
                TotalFiles = manifest.Files.Count,
            });

            FileTreeCopier.Copy(manifest, destination, request.PreserveAttributes,
                request.AllowMergeIntoExisting, ct,
                progress is null ? null : new Progress<CopyProgress>(p => progress.Report(new MigrationProgress
                {
                    Phase = MigrationPhase.Copying,
                    Message = "正在复制…",
                    TotalBytes = p.TotalBytes,
                    ProcessedBytes = p.CopiedBytes,
                    TotalFiles = p.TotalFiles,
                    ProcessedFiles = p.CopiedFiles,
                    CurrentFile = p.CurrentFile,
                })),
                log.Info,
                createdFiles);

            // ---------- 5. 校验 ----------
            DestinationTamperHookForTests?.Invoke(destination);

            entry.Phase = MigrationPhase.Verifying;
            journal.Write(entry);

            progress?.Report(new MigrationProgress { Phase = MigrationPhase.Verifying, Message = "正在校验副本…" });

            var verify = TreeVerifier.Verify(manifest, destination, request.VerifyLevel, ct,
                progress is null ? null : new Progress<VerifyProgress>(p => progress.Report(new MigrationProgress
                {
                    Phase = MigrationPhase.Verifying,
                    Message = "正在校验副本…",
                    VerifyPercent = p.Total > 0 ? (double)p.Checked / p.Total * 100 : 0,
                    CurrentFile = p.Current,
                })));

            foreach (var line in TreeVerifier.Describe(verify)) log.Info(line);

            if (!verify.Passed)
            {
                // 源目录还没被动过，这是最干净的失败路径。
                log.Error("校验未通过，回滚：删掉目标副本，源目录保持原样。");
                var rb = DeleteCopiedData(destination, destinationExistedBefore, createdFiles, log, notes);
                journal.Complete();

                var failedVerify = BuildResult(request, rb == RollbackState.Failed
                        ? MigrationOutcome.FailedRollbackIncomplete
                        : MigrationOutcome.FailedRolledBack,
                    MigrationPhase.RolledBack, rb, journal.JobId, log, preflight: preflight,
                    verify: verify, notes: notes,
                    failure: new MigrationFailure(MigrationPhase.Verifying, "副本校验", verify.Summary, null, destination));
                Record(request, failedVerify, log.Path);
                return failedVerify;
            }

            // ---------- 6. 写入来源标记 ----------
            // 必须在改名之前写，这样源目录的内容里就带着"我从哪来"的信息。
            OriginMarkerStore.Write(destination, OriginMarkerStore.Create(source, destination, request.LinkKind,
                manifest.TotalBytes, manifest.Files.Count, "由搬家模式创建"));
            log.Info("已在目标目录写入来源标记。");

            // ---------- 7. 腾出原位置 ----------
            entry.Phase = MigrationPhase.RenamingSource;
            journal.Write(entry);
            progress?.Report(new MigrationProgress
            {
                Phase = MigrationPhase.RenamingSource,
                Message = "正在腾出原位置…",
            });

            log.Info($"把源目录改名为备份：{source} -> {backupPath}");
            Directory.Move(source, backupPath);
            sourceRenamed = true;
            log.Info("源目录已改名。");

            // ---------- 8. 建立链接 ----------
            entry.Phase = MigrationPhase.Linking;
            entry.BackupPath = backupPath;
            journal.Write(entry);

            progress?.Report(new MigrationProgress
            {
                Phase = MigrationPhase.Linking,
                Message = $"正在建立{(request.LinkKind == LinkKind.Junction ? "目录联接" : "符号链接")}…",
            });

            try
            {
                LinkService.Create(source, destination, request.LinkKind);
            }
            catch (Exception linkEx)
            {
                // 这是最需要回滚的时刻：源目录已经被改名了。
                log.Error($"建立链接失败：{linkEx.Message}");
                var rb = RollbackRenameAndCopy(source, backupPath, destination, destinationExistedBefore,
                    createdFiles, sourceRenamed, linkCreated, log, notes);

                journal.Complete();
                var result = BuildResult(request,
                    rb == RollbackState.Failed ? MigrationOutcome.FailedRollbackIncomplete : MigrationOutcome.FailedRolledBack,
                    rb == RollbackState.Failed ? MigrationPhase.RollbackFailed : MigrationPhase.RolledBack,
                    rb, journal.JobId, log, preflight: preflight, notes: notes,
                    failure: Describe(MigrationPhase.Linking, "建立链接", linkEx));
                Record(request, result, log.Path);
                return result;
            }

            linkCreated = true;
            log.Info($"链接已建立：{source} -> {destination}");

            // ---------- 9. 确认链接真的通 ----------
            if (!VerifyLink(source, destination, log, out var linkProblem))
            {
                log.Error("链接自检失败：" + linkProblem);
                var rb = RollbackRenameAndCopy(source, backupPath, destination, destinationExistedBefore,
                    createdFiles, sourceRenamed, linkCreated, log, notes);

                journal.Complete();
                var result = BuildResult(request,
                    rb == RollbackState.Failed ? MigrationOutcome.FailedRollbackIncomplete : MigrationOutcome.FailedRolledBack,
                    rb == RollbackState.Failed ? MigrationPhase.RollbackFailed : MigrationPhase.RolledBack,
                    rb, journal.JobId, log, preflight: preflight, verify: verify, notes: notes,
                    failure: new MigrationFailure(MigrationPhase.Linking, "链接自检", linkProblem, null, source));
                Record(request, result, log.Path);
                return result;
            }

            entry.LinkCreated = true;
            journal.Write(entry);

            // ---------- ⛔ 回滚边界在这里 ⛔ ----------
            // 从这一行开始，目标副本已校验通过、链接已验证可用。
            // 数据已经安全地躺在目标盘上，任何后续问题都只做"收尾"，绝不再回滚。

            // ---------- 10. 收尾：删除备份 ----------
            entry.Phase = MigrationPhase.Committing;
            journal.Write(entry);

            if (request.KeepBackupAfterSuccess)
            {
                log.Info("按用户要求保留原数据备份：" + backupPath);
                notes.Add($"原数据已按设置保留在：{backupPath}（确认无误后可以手动删除）");
            }
            else
            {
                progress?.Report(new MigrationProgress
                {
                    Phase = MigrationPhase.Committing,
                    Message = "正在清理原盘上的旧数据…",
                });

                if (!DeleteDirectorySafely(backupPath, log))
                {
                    log.Warn("旧数据没能完全删除，但不影响使用（链接与目标副本都正常）。");
                    notes.Add($"⚠ 原盘的旧数据未能完全删除，请稍后手动清理：{backupPath}");
                }
                else
                {
                    log.Info("旧数据已清理。");
                }
            }

            entry.Phase = MigrationPhase.Completed;
            entry.Finished = true;
            journal.Write(entry);
            journal.Complete();

            log.Info("作业成功完成。");
            var success = BuildResult(request, MigrationOutcome.Success, MigrationPhase.Completed,
                RollbackState.NotNeeded, journal.JobId, log, preflight: preflight, verify: verify,
                notes: notes, bytesMoved: manifest.TotalBytes, filesMoved: manifest.Files.Count,
                actualDestination: destination);
            Record(request, success, log.Path);
            return success;
        }
        catch (OperationCanceledException)
        {
            log.Warn("作业被取消，开始回滚。");
            var rb = RollbackRenameAndCopy(source, backupPath, destination, destinationExistedBefore,
                createdFiles, sourceRenamed, linkCreated, log, notes);
            journal.Complete();

            var cancelled = BuildResult(request, MigrationOutcome.Cancelled,
                rb == RollbackState.Failed ? MigrationPhase.RollbackFailed : MigrationPhase.RolledBack,
                rb, journal.JobId, log, preflight: preflight, notes: notes,
                description: "用户取消了操作，已回滚。");
            Record(request, cancelled, log.Path);
            return cancelled;
        }
        catch (Exception ex)
        {
            log.Error($"搬运阶段失败：{ex.Message}");
            var rb = RollbackRenameAndCopy(source, backupPath, destination, destinationExistedBefore,
                createdFiles, sourceRenamed, linkCreated, log, notes);
            journal.Complete();

            var result = BuildResult(request,
                rb == RollbackState.Failed ? MigrationOutcome.FailedRollbackIncomplete : MigrationOutcome.FailedRolledBack,
                rb == RollbackState.Failed ? MigrationPhase.RollbackFailed : MigrationPhase.RolledBack,
                rb, journal.JobId, log, preflight: preflight, notes: notes,
                failure: Describe(entry.Phase, "搬运", ex));
            Record(request, result, log.Path);
            return result;
        }
    }

    // ==================================================================
    //  回滚原语
    // ==================================================================

    /// <summary>
    /// 回滚"搬家"作业：撤链接 → 备份改回原名 → 删目标副本。
    /// 顺序很重要：先把原位置腾出来（撤链接），再把数据放回去，最后才删目标副本。
    /// </summary>
    private static RollbackState RollbackRenameAndCopy(
        string source, string backupPath, string destination, bool destinationExistedBefore,
        List<string> createdFiles, bool sourceRenamed, bool linkCreated,
        MigrationLog log, List<string> notes)
    {
        bool ok = true;

        // 1) 拆掉链接
        if (linkCreated || ReparsePoint.GetLinkKind(source) != LinkKind.None)
        {
            try
            {
                if (LinkService.TryDeleteLink(source))
                    log.Info("回滚：已拆除链接。");
                else
                {
                    // 链接没了也可能已经被删掉了，不算致命。
                    if (ReparsePoint.GetLinkKind(source) != LinkKind.None)
                    {
                        log.Error("回滚：链接拆除失败。" + source);
                        notes.Add($"⚠ 无法拆除链接，请手动删除：{source}");
                        ok = false;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("回滚：拆除链接时异常：" + ex.Message);
                notes.Add($"⚠ 无法拆除链接，请手动删除：{source}");
                ok = false;
            }
        }

        // 2) 备份改回原名
        if (sourceRenamed && Directory.Exists(backupPath))
        {
            try
            {
                if (Directory.Exists(source) || File.Exists(source))
                {
                    // 正常情况下链接已经拆掉了，这里不该还有东西。
                    if (ReparsePoint.IsDirectoryEmpty(source))
                        Directory.Delete(source, false);
                }

                Directory.Move(backupPath, source);
                log.Info($"回滚：原目录已恢复：{backupPath} -> {source}");
            }
            catch (Exception ex)
            {
                log.Error($"回滚：原名恢复失败：{ex.Message}");
                notes.Add($"⚠ 原目录还留在备份路径，请手动改回：{backupPath} -> {source}");
                ok = false;
            }
        }
        else if (sourceRenamed && !Directory.Exists(backupPath) && !Directory.Exists(source))
        {
            // 既没有备份也没有原目录 —— 说明改名这一步本身没成功。
            log.Warn("回滚：未发现备份目录，源目录可能未被改名。");
        }

        // 3) 删掉目标副本（只有在源目录已经成功恢复之后，这一步才是安全的）
        if (ok)
        {
            var rb = DeleteCopiedData(destination, destinationExistedBefore, createdFiles, log, notes);
            if (rb == RollbackState.Failed) ok = false;
        }
        else
        {
            notes.Add($"⚠ 目标副本暂未删除，确认源目录恢复后可手动删除：{destination}");
        }

        return ok ? RollbackState.Succeeded : RollbackState.Failed;
    }

    /// <summary>
    /// 删除复制到目标位置的数据。
    /// <para>如果目标目录是本次新建的，整棵删掉；如果是合并进已有目录，
    /// 只删我们自己写进去的那些文件，绝不碰用户原有的东西。</para>
    /// </summary>
    private static RollbackState DeleteCopiedData(
        string destination, bool destinationExistedBefore, List<string> createdFiles,
        MigrationLog log, List<string> notes)
    {
        try
        {
            if (!Directory.Exists(destination)) return RollbackState.NotNeeded;

            if (!destinationExistedBefore)
            {
                log.Info("回滚：删除本次新建的目标副本目录：" + destination);
                if (DeleteDirectorySafely(destination, log))
                    return RollbackState.Succeeded;

                notes.Add($"⚠ 目标副本未能完全删除，请手动清理：{destination}");
                return RollbackState.Failed;
            }

            // 合并模式：只删我们创建的
            log.Info($"回滚：目标目录先前已存在，只删除本次写入的 {createdFiles.Count} 个文件。");
            int failedCount = 0;

            foreach (var file in createdFiles)
            {
                try
                {
                    if (!File.Exists(file)) continue;
                    var attrs = File.GetAttributes(file);
                    if ((attrs & FileAttributes.ReadOnly) != 0)
                        File.SetAttributes(file, attrs & ~FileAttributes.ReadOnly);
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    failedCount++;
                    log.Warn($"回滚：删除失败 {file}：{ex.Message}");
                }
            }

            if (failedCount > 0)
            {
                notes.Add($"⚠ 有 {failedCount} 个本次写入的文件没能删除，请手动检查：{destination}");
                return RollbackState.Failed;
            }

            return RollbackState.Succeeded;
        }
        catch (Exception ex)
        {
            log.Error("回滚：删除目标副本时异常：" + ex.Message);
            notes.Add($"⚠ 目标副本删除失败，请手动清理：{destination}");
            return RollbackState.Failed;
        }
    }

    /// <summary>
    /// 安全地递归删除一个目录。
    /// <para><b>关键点：遇到重解析点只删链接本身，绝不递归进去。</b>
    /// 否则一个指向 D 盘的目录联接会让"清理 C 盘旧数据"变成清空 D 盘。</para>
    /// </summary>
    internal static bool DeleteDirectorySafely(string path, MigrationLog? log = null)
    {
        if (!Directory.Exists(path)) return true;

        try
        {
            var attrs = File.GetAttributes(path);

            if ((attrs & FileAttributes.ReparsePoint) != 0)
            {
                // 这个路径本身就是链接：只删链接。
                Directory.Delete(path, recursive: false);
                return true;
            }

            if ((attrs & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(path, attrs & ~FileAttributes.ReadOnly);

            foreach (var entry in Directory.EnumerateFileSystemEntries(path))
            {
                try
                {
                    var entryAttrs = File.GetAttributes(entry);

                    if ((entryAttrs & FileAttributes.ReparsePoint) != 0)
                    {
                        // 链接：只删链接，不跟进去。
                        if ((entryAttrs & FileAttributes.Directory) != 0) Directory.Delete(entry, false);
                        else File.Delete(entry);
                        log?.Info($"已删除嵌套链接（未跟随）：{entry}");
                        continue;
                    }

                    if ((entryAttrs & FileAttributes.Directory) != 0)
                    {
                        DeleteDirectorySafely(entry, log);
                    }
                    else
                    {
                        if ((entryAttrs & FileAttributes.ReadOnly) != 0)
                            File.SetAttributes(entry, entryAttrs & ~FileAttributes.ReadOnly);
                        File.Delete(entry);
                    }
                }
                catch (Exception ex)
                {
                    log?.Warn($"删除失败 {entry}：{ex.Message}");
                    return false;
                }
            }

            Directory.Delete(path, recursive: false);
            return true;
        }
        catch (Exception ex)
        {
            log?.Warn($"删除目录失败 {path}：{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 透过链接真实地读一次，确认链接确实指向目标并且内容可见。
    /// 光看"是不是重解析点"不够 —— 指向一个不存在的地方同样是坏的。
    /// </summary>
    private static bool VerifyLink(string linkPath, string targetPath, MigrationLog log, out string problem)
    {
        problem = string.Empty;

        var kind = ReparsePoint.GetLinkKind(linkPath);
        if (kind == LinkKind.None)
        {
            problem = "原位置没有变成链接。";
            return false;
        }

        var actual = ReparsePoint.ReadLinkTarget(linkPath);
        if (actual is null)
        {
            problem = "无法读取链接目标。";
            return false;
        }

        if (!string.Equals(actual.TrimEnd('\\'), targetPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        {
            problem = $"链接指向了错误的位置：期望 {targetPath}，实际 {actual}。";
            return false;
        }

        // 透过链接列目录，确认能读到东西。
        try
        {
            int viaLink = Directory.EnumerateFileSystemEntries(linkPath).Take(64).Count();
            int direct = Directory.EnumerateFileSystemEntries(targetPath).Take(64).Count();

            if (viaLink != direct)
            {
                problem = $"透过链接读到的条目数（{viaLink}）与目标目录（{direct}）不一致。";
                return false;
            }
        }
        catch (Exception ex)
        {
            problem = $"透过链接访问失败：{ex.Message}";
            return false;
        }

        log.Info($"链接自检通过：{linkPath} -> {targetPath}");
        return true;
    }

    // ==================================================================
    //  结果组装
    // ==================================================================

    private static MigrationResult BuildResult(
        MigrationRequest request,
        MigrationOutcome outcome,
        MigrationPhase finalPhase,
        RollbackState rollback,
        string jobId,
        MigrationLog log,
        PreflightReport? preflight = null,
        VerifyReport? verify = null,
        MigrationFailure? failure = null,
        List<string>? notes = null,
        long bytesMoved = 0,
        long filesMoved = 0,
        string? actualDestination = null,
        string? description = null)
    {
        var allNotes = notes ?? [];
        if (!string.IsNullOrWhiteSpace(description)) allNotes.Insert(0, description);

        return new MigrationResult
        {
            Kind = request.Kind,
            Mode = request.Mode,
            SourcePath = request.SourcePath,
            DestinationPath = request.DestinationPath,
            Outcome = outcome,
            FinalPhase = finalPhase,
            Rollback = rollback,
            Preflight = preflight,
            Verify = verify,
            Failure = failure,
            BytesMoved = bytesMoved,
            FilesMoved = filesMoved,
            Notes = allNotes,
            LogPath = log.Path,
            Elapsed = log.Elapsed,
            ActualDestinationPath = actualDestination,
        };
    }

    private static MigrationFailure Describe(MigrationPhase phase, string operation, Exception ex)
    {
        int? code = ex is Win32Exception w32 ? w32.NativeErrorCode
            : ex.HResult != 0 ? ex.HResult : null;

        return new MigrationFailure(phase, operation, ex.Message, code, null);
    }

    private static void Record(MigrationRequest request, MigrationResult result, string logPath)
    {
        HistoryStore.Append(new MigrationRecord
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Kind = result.Kind,
            Mode = result.Mode,
            Outcome = result.Outcome,
            LinkKind = request.LinkKind,
            SourcePath = result.SourcePath,
            DestinationPath = result.DestinationPath,
            ActualDestinationPath = result.ActualDestinationPath,
            BytesMoved = result.BytesMoved,
            FilesMoved = result.FilesMoved,
            ElapsedSeconds = result.Elapsed.TotalSeconds,
            FailureMessage = result.Failure?.Message,
            Rollback = result.Rollback,
            LogPath = logPath,
            Label = request.Label,
        });
    }
}
