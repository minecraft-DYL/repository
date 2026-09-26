using Wyolm.Core.Interop;
using Wyolm.Core.Models;
using Wyolm.Core.Services;

namespace Wyolm.Core.Tests;

/// <summary>
/// 崩溃恢复。
/// <para>这里的做法是：手工把磁盘布置成"某个时刻断电了"的样子，再写一条对应的事务日志，
/// 然后跑恢复逻辑，断言它做出了正确的判断（该回滚的回滚，该收尾的收尾）。</para>
/// </summary>
public sealed class RecoveryServiceTests
{
    /// <summary>造一个"源目录已改名为备份、链接还没建"的现场。</summary>
    private static (string Source, string Backup, string Destination) ArrangeInterruptedAfterRename(
        TestSandbox sb, string jobId)
    {
        var parent = sb.Dir("c");
        var source = Path.Combine(parent, "BigFolder");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "a.txt"), "payload");

        var destination = Path.Combine(sb.TargetVolume, "BigFolder");
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(destination, "a.txt"), "payload");

        var backup = Path.Combine(parent, $".BigFolder.wyolm-backup-{jobId[^6..]}");
        Directory.Move(source, backup);

        using var journal = new MigrationJournal(jobId);
        journal.Write(new JournalEntry
        {
            JobId = jobId,
            Phase = MigrationPhase.Linking,
            Mode = MigrationMode.MoveAndLink,
            SourcePath = source,
            DestinationPath = destination,
            BackupPath = backup,
            CopiedPath = destination,
            DestinationExistedBefore = false,
        });

        return (source, backup, destination);
    }

    [Fact]
    public void Interrupted_before_linking_is_rolled_back_completely()
    {
        using var sb = new TestSandbox();
        var (source, backup, destination) = ArrangeInterruptedAfterRename(sb, "job-rollback-1");

        var report = RecoveryService.Recover();

        Assert.Single(report.Items);
        var item = report.Items[0];
        Assert.Equal(RecoveryAction.RollBack, item.Action);
        Assert.True(item.Succeeded, item.Detail);

        // 原目录回到原位，内容完好
        Assert.True(Directory.Exists(source));
        Assert.Equal(LinkKind.None, ReparsePoint.GetLinkKind(source));
        Assert.Equal("payload", File.ReadAllText(Path.Combine(source, "a.txt")));

        // 备份目录消失，半成品副本被清掉
        Assert.False(Directory.Exists(backup));
        Assert.False(Directory.Exists(destination));

        // 事务日志被清理，下次启动不会再处理它
        Assert.Empty(MigrationJournal.LoadUnfinished());
    }

    /// <summary>
    /// 链接已经建好的情况下中断：数据已经安全落到目标盘，恢复逻辑应该"收尾"而不是回滚
    /// —— 因为这时候去回滚只会把一份正在被使用的数据搬来搬去。
    /// </summary>
    [Fact]
    public void Interrupted_after_linking_is_finished_not_rolled_back()
    {
        using var sb = new TestSandbox();

        var parent = sb.Dir("c");
        var source = Path.Combine(parent, "BigFolder");
        var destination = Path.Combine(sb.TargetVolume, "BigFolder");
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(destination, "a.txt"), "payload");

        // 链接已建好
        LinkService.Create(source, destination, LinkKind.Junction);

        // 备份里还残留着旧数据（收尾删了一半就断电）
        var backup = Path.Combine(parent, ".BigFolder.wyolm-backup-abcdef");
        Directory.CreateDirectory(backup);
        File.WriteAllText(Path.Combine(backup, "a.txt"), "payload");

        const string jobId = "job-finish-1";
        using (var journal = new MigrationJournal(jobId))
        {
            journal.Write(new JournalEntry
            {
                JobId = jobId,
                Phase = MigrationPhase.Committing,
                Mode = MigrationMode.MoveAndLink,
                SourcePath = source,
                DestinationPath = destination,
                BackupPath = backup,
                CopiedPath = destination,
                DestinationExistedBefore = false,
                LinkCreated = true,
            });
        }

        var report = RecoveryService.Recover();

        Assert.Single(report.Items);
        Assert.Equal(RecoveryAction.FinishCommit, report.Items[0].Action);
        Assert.True(report.Items[0].Succeeded, report.Items[0].Detail);

        // 链接和链接指向的数据都必须还在
        Assert.Equal(LinkKind.Junction, ReparsePoint.GetLinkKind(source));
        Assert.True(LinkService.PointsTo(source, destination));
        Assert.Equal("payload", File.ReadAllText(Path.Combine(source, "a.txt")));

        // 残留的旧数据被删干净
        Assert.False(Directory.Exists(backup));
    }

    /// <summary>复制阶段就断了：源目录根本没被动过，只需要清掉目标上的半成品。</summary>
    [Fact]
    public void Interrupted_during_copy_only_cleans_up_the_partial_copy()
    {
        using var sb = new TestSandbox();

        var source = sb.Dir(@"c\BigFolder");
        sb.Write(@"c\BigFolder\a.txt", "payload");

        var destination = Path.Combine(sb.TargetVolume, "BigFolder");
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(destination, "a.txt"), "half copied");

        const string jobId = "job-copy-1";
        using (var journal = new MigrationJournal(jobId))
        {
            journal.Write(new JournalEntry
            {
                JobId = jobId,
                Phase = MigrationPhase.Copying,
                Mode = MigrationMode.MoveAndLink,
                SourcePath = source,
                DestinationPath = destination,
                CopiedPath = destination,
                DestinationExistedBefore = false,
            });
        }

        var report = RecoveryService.Recover();

        Assert.Single(report.Items);
        Assert.Equal(RecoveryAction.CleanUpOnly, report.Items[0].Action);
        Assert.True(report.Items[0].Succeeded, report.Items[0].Detail);

        Assert.True(File.Exists(Path.Combine(source, "a.txt")));
        Assert.False(Directory.Exists(destination));
    }

    /// <summary>
    /// 合并模式（目标目录原本就有内容）下中断：不能自动删目标，必须交给人工确认。
    /// 这是"宁可留一份多余的数据，也不误删用户的文件"的取舍。
    /// </summary>
    [Fact]
    public void Interrupted_merge_job_is_handed_to_manual_review()
    {
        using var sb = new TestSandbox();

        var source = sb.Dir(@"c\BigFolder");
        sb.Write(@"c\BigFolder\a.txt", "payload");

        var destination = sb.Dir(@"d\BigFolder");
        sb.Write(@"d\BigFolder\user-original.txt", "绝对不能删");

        const string jobId = "job-merge-1";
        using (var journal = new MigrationJournal(jobId))
        {
            journal.Write(new JournalEntry
            {
                JobId = jobId,
                Phase = MigrationPhase.Verifying,
                Mode = MigrationMode.MoveAndLink,
                SourcePath = source,
                DestinationPath = destination,
                CopiedPath = destination,
                DestinationExistedBefore = true,
            });
        }

        var report = RecoveryService.Recover();

        Assert.Single(report.Items);
        Assert.Equal(RecoveryAction.NeedsManualReview, report.Items[0].Action);
        Assert.Equal(1, report.NeedsManual);

        // 用户原有的文件必须原封不动地留着
        Assert.True(File.Exists(Path.Combine(destination, "user-original.txt")));
        Assert.Equal("绝对不能删", File.ReadAllText(Path.Combine(destination, "user-original.txt")));

        // 源目录也没被动过
        Assert.True(File.Exists(Path.Combine(source, "a.txt")));
    }

    /// <summary>链接模式中断：只可能留下一个没建好的链接，绝不该去碰目标里的真实数据。</summary>
    [Fact]
    public void Interrupted_link_only_job_never_touches_the_real_data()
    {
        using var sb = new TestSandbox();

        var real = sb.Dir(@"d\RealFolder");
        sb.Write(@"d\RealFolder\important.txt", "真实数据");

        var linkPlace = sb.Path_(@"c\Linked");

        const string jobId = "job-linkonly-1";
        using (var journal = new MigrationJournal(jobId))
        {
            journal.Write(new JournalEntry
            {
                JobId = jobId,
                Phase = MigrationPhase.Linking,
                Mode = MigrationMode.LinkOnly,
                SourcePath = linkPlace,
                DestinationPath = real,
                // 关键：链接模式绝不能把 CopiedPath 指向真实数据
                CopiedPath = null,
                DestinationExistedBefore = true,
            });
        }

        var report = RecoveryService.Recover();

        Assert.Single(report.Items);
        Assert.True(report.Items[0].Succeeded, report.Items[0].Detail);

        // 真实数据必须还在
        Assert.True(Directory.Exists(real));
        Assert.Equal("真实数据", File.ReadAllText(Path.Combine(real, "important.txt")));
    }

    [Fact]
    public void No_journal_means_nothing_to_recover()
    {
        using var sb = new TestSandbox();
        _ = sb;

        var report = RecoveryService.Recover();

        Assert.False(report.HasWork);
        Assert.Equal(0, report.NeedsManual);
    }
}
