using Wyolm.Core.Interop;
using Wyolm.Core.Models;
using Wyolm.Core.Services;

namespace Wyolm.Core.Tests;

/// <summary>迁移引擎：搬家成功路径、失败回滚、以及崩溃恢复。</summary>
public sealed class MigrationEngineTests
{
    private static MigrationRequest Request(string source, string destination, MigrationMode mode,
        VerifyLevel level = VerifyLevel.ContentHash) => new()
        {
            SourcePath = source,
            DestinationPath = destination,
            Mode = mode,
            LinkKind = LinkKind.Junction,
            VerifyLevel = level,
            Kind = MigrationJobKind.Custom,
            SkipLockCheck = true,
        };

    // ==================================================================
    //  成功路径
    // ==================================================================
    [Fact]
    public async Task Move_and_link_moves_data_creates_a_link_and_cleans_up()
    {
        using var sb = new TestSandbox();
        var source = sb.Dir(@"c\BigFolder");
        sb.Write(@"c\BigFolder\a.txt", "hello");
        sb.Write(@"c\BigFolder\nested\b.txt", new string('b', 4096));
        sb.Write(@"c\BigFolder\nested\中文名.txt", "中文内容");

        // File.WriteAllText 会带 UTF-8 BOM，所以长度要以磁盘上的实际字节数为准
        var expectedNestedLength = new FileInfo(sb.Path_(@"c\BigFolder\nested\b.txt")).Length;

        var destination = Path.Combine(sb.TargetVolume, "BigFolder");

        var result = await new MigrationEngine().ExecuteAsync(Request(source, destination, MigrationMode.MoveAndLink));

        Assert.Equal(MigrationOutcome.Success, result.Outcome);
        Assert.True(result.Succeeded);

        // 原位置变成了链接，并且指向目标
        Assert.Equal(LinkKind.Junction, ReparsePoint.GetLinkKind(source));
        Assert.True(LinkService.PointsTo(source, destination));

        // 透过链接读到的就是搬走后的数据
        Assert.Equal("hello", File.ReadAllText(Path.Combine(source, "a.txt")));
        Assert.Equal(expectedNestedLength, new FileInfo(Path.Combine(source, "nested", "b.txt")).Length);
        Assert.Equal("中文内容", File.ReadAllText(Path.Combine(source, "nested", "中文名.txt")));

        // 目标里有来源标记，以后才能接回来
        var marker = OriginMarkerStore.Read(destination);
        Assert.NotNull(marker);
        Assert.Equal(PathGuard.Normalize(source), PathGuard.Normalize(marker!.OriginalPath));
        Assert.Equal(PathGuard.Normalize(destination), PathGuard.Normalize(marker.CurrentPath));

        // 原盘上的临时备份目录必须清理干净
        var parent = Path.GetDirectoryName(source)!;
        Assert.Empty(Directory.GetDirectories(parent, "*.wyolm-backup-*"));

        // 校验报告应该是通过的
        Assert.NotNull(result.Verify);
        Assert.True(result.Verify!.Passed);
    }

    [Fact]
    public async Task Copy_only_leaves_the_source_untouched_and_still_writes_a_marker()
    {
        using var sb = new TestSandbox();
        var source = sb.Dir(@"c\Folder");
        sb.Write(@"c\Folder\a.txt", "data");

        var destination = Path.Combine(sb.TargetVolume, "FolderCopy");

        var result = await new MigrationEngine().ExecuteAsync(Request(source, destination, MigrationMode.CopyOnly));

        Assert.Equal(MigrationOutcome.Success, result.Outcome);

        // 源还是真实目录，数据还在
        Assert.Equal(LinkKind.None, ReparsePoint.GetLinkKind(source));
        Assert.True(File.Exists(Path.Combine(source, "a.txt")));

        // 目标也有一份
        Assert.True(File.Exists(Path.Combine(destination, "a.txt")));

        var marker = OriginMarkerStore.Read(destination);
        Assert.NotNull(marker);
        Assert.Equal(PathGuard.Normalize(source), PathGuard.Normalize(marker!.OriginalPath));
    }

    [Fact]
    public async Task Dry_run_does_not_touch_anything()
    {
        using var sb = new TestSandbox();
        var source = sb.Dir(@"c\Folder");
        sb.Write(@"c\Folder\a.txt", "data");
        var destination = Path.Combine(sb.TargetVolume, "Folder");

        var request = new MigrationRequest
        {
            SourcePath = source,
            DestinationPath = destination,
            Mode = MigrationMode.MoveAndLink,
            DryRun = true,
            SkipLockCheck = true,
        };

        var result = await new MigrationEngine().ExecuteAsync(request);

        Assert.Equal(MigrationOutcome.DryRunPassed, result.Outcome);
        Assert.Equal(LinkKind.None, ReparsePoint.GetLinkKind(source));
        Assert.False(Directory.Exists(destination));
    }

    [Fact]
    public async Task Preflight_rejection_leaves_everything_alone()
    {
        using var sb = new TestSandbox();
        var source = sb.Dir(@"c\Folder");
        sb.Write(@"c\Folder\a.txt", "data");

        // 目标被别的东西占着，且没开合并
        var destination = sb.Dir(@"d\Folder");
        sb.Write(@"d\Folder\existing.txt", "existing");

        var result = await new MigrationEngine().ExecuteAsync(Request(source, destination, MigrationMode.MoveAndLink));

        Assert.Equal(MigrationOutcome.Rejected, result.Outcome);
        Assert.Equal(RollbackState.NotNeeded, result.Rollback);
        Assert.Equal(LinkKind.None, ReparsePoint.GetLinkKind(source));
        Assert.True(File.Exists(Path.Combine(source, "a.txt")));
        Assert.True(File.Exists(Path.Combine(destination, "existing.txt")));
    }

    // ==================================================================
    //  失败 → 自动回滚
    // ==================================================================

    /// <summary>
    /// 复制阶段失败：目标位置有个文件被独占打开，复制必然读不到。
    /// 期望结果是"回滚成功"——源目录一个字节都没少，目标上的半成品被清掉。
    /// </summary>
    [Fact]
    public async Task Copy_failure_rolls_back_and_leaves_the_source_intact()
    {
        using var sb = new TestSandbox();
        var source = sb.Dir(@"c\Locked");
        sb.Write(@"c\Locked\a.txt", "aaa");
        var lockedFile = sb.Write(@"c\Locked\b.txt", "this one is locked");

        var destination = Path.Combine(sb.TargetVolume, "Locked");

        // 独占打开（FileShare.None），复制器读它时必然抛 IOException
        using var hold = new FileStream(lockedFile, FileMode.Open, FileAccess.Read, FileShare.None);

        var result = await new MigrationEngine().ExecuteAsync(Request(source, destination, MigrationMode.MoveAndLink));

        Assert.Equal(MigrationOutcome.FailedRolledBack, result.Outcome);
        Assert.Equal(RollbackState.Succeeded, result.Rollback);

        // 源目录完好：还是真实目录，文件都在
        Assert.Equal(LinkKind.None, ReparsePoint.GetLinkKind(source));
        Assert.True(File.Exists(Path.Combine(source, "a.txt")));
        Assert.True(File.Exists(Path.Combine(source, "b.txt")));

        // 目标上的半成品被清理干净
        Assert.False(Directory.Exists(destination));
    }

    /// <summary>
    /// 建链接阶段失败：这是最危险的时刻 —— 源目录已经被改名腾位了。
    /// 用非法的链接类型做故障注入，精确打中"改名之后、链接之前"这个窗口，
    /// 验证回滚能把原目录完整还原、并且把一个字节都不剩地撤销目标副本。
    /// </summary>
    [Fact]
    public async Task Failure_after_renaming_the_source_restores_the_original_folder()
    {
        using var sb = new TestSandbox();
        var source = sb.Dir(@"c\BigFolder");
        sb.Write(@"c\BigFolder\a.txt", "hello");
        sb.Write(@"c\BigFolder\nested\b.txt", new string('b', 8192));

        var expectedNestedLength = new FileInfo(sb.Path_(@"c\BigFolder\nested\b.txt")).Length;

        var destination = Path.Combine(sb.TargetVolume, "BigFolder");

        var request = new MigrationRequest
        {
            SourcePath = source,
            DestinationPath = destination,
            Mode = MigrationMode.MoveAndLink,
            LinkKind = (LinkKind)99, // 故障注入：建链接时必然抛异常
            VerifyLevel = VerifyLevel.SizeAndTimestamp,
            SkipLockCheck = true,
        };

        var result = await new MigrationEngine().ExecuteAsync(request);

        Assert.Equal(MigrationOutcome.FailedRolledBack, result.Outcome);
        Assert.Equal(RollbackState.Succeeded, result.Rollback);

        // 原目录回到原位，内容一字不少
        Assert.Equal(LinkKind.None, ReparsePoint.GetLinkKind(source));
        Assert.True(Directory.Exists(source));
        Assert.Equal("hello", File.ReadAllText(Path.Combine(source, "a.txt")));
        Assert.Equal(expectedNestedLength, new FileInfo(Path.Combine(source, "nested", "b.txt")).Length);

        // 目标副本和临时备份都不该留下
        Assert.False(Directory.Exists(destination));
        var parent = Path.GetDirectoryName(source)!;
        Assert.Empty(Directory.GetDirectories(parent, "*.wyolm-backup-*"));
    }

    /// <summary>
    /// 校验失败必须回滚，而且必须发生在源目录被动过之前。
    /// 通过引擎的测试注入点，在复制完成后偷偷把副本改坏，逼出这条分支。
    /// </summary>
    [Fact]
    public async Task Verification_failure_removes_the_copy_and_keeps_the_source()
    {
        using var sb = new TestSandbox();
        var source = sb.Dir(@"c\Folder");
        sb.Write(@"c\Folder\a.txt", "content");
        sb.Write(@"c\Folder\sub\b.txt", new string('b', 2048));
        var expectedNestedLength = new FileInfo(sb.Path_(@"c\Folder\sub\b.txt")).Length;
        var destination = Path.Combine(sb.TargetVolume, "Folder");

        // 故障注入：复制完成后，把副本里一个文件的内容换成等长的另一种内容。
        // 等长是关键 —— 这样只有"全量哈希"这一档能识破，能顺带证明它确实在干活。
        MigrationEngine.DestinationTamperHookForTests = dest =>
        {
            var victim = Path.Combine(dest, "sub", "b.txt");
            if (File.Exists(victim))
            {
                var stamp = File.GetLastWriteTimeUtc(victim);
                File.WriteAllText(victim, new string('Z', 2048));
                File.SetLastWriteTimeUtc(victim, stamp);
            }
        };

        try
        {
            var request = new MigrationRequest
            {
                SourcePath = source,
                DestinationPath = destination,
                Mode = MigrationMode.MoveAndLink,
                VerifyLevel = VerifyLevel.ContentHash,
                SkipLockCheck = true,
            };

            var result = await new MigrationEngine().ExecuteAsync(request);

            // 校验失败的判定
            Assert.Equal(MigrationOutcome.FailedRolledBack, result.Outcome);
            Assert.Equal(RollbackState.Succeeded, result.Rollback);
            Assert.NotNull(result.Verify);
            Assert.False(result.Verify!.Passed);
            Assert.Contains(result.Verify.Mismatches, m => m.Reason.Contains("哈希"));

            // 源目录必须一个字节都没变，而且还是个真实目录（没被改成链接）
            Assert.Equal(LinkKind.None, ReparsePoint.GetLinkKind(source));
            Assert.Equal("content", File.ReadAllText(Path.Combine(source, "a.txt")));
            Assert.Equal(expectedNestedLength, new FileInfo(Path.Combine(source, "sub", "b.txt")).Length);
            Assert.Equal('b', File.ReadAllText(Path.Combine(source, "sub", "b.txt"))[0]);

            // 坏掉的副本必须被清理干净
            Assert.False(Directory.Exists(destination));

            // 也不能留下临时备份
            var parent = Path.GetDirectoryName(source)!;
            Assert.Empty(Directory.GetDirectories(parent, "*.wyolm-backup-*"));
        }
        finally
        {
            MigrationEngine.DestinationTamperHookForTests = null;
        }
    }

    // ==================================================================
    //  标记 → 接回原位 的完整闭环
    // ==================================================================
    [Fact]
    public async Task A_moved_folder_can_be_found_on_another_drive_and_linked_back()
    {
        using var sb = new TestSandbox();
        var source = sb.Dir(@"c\MyCache");
        sb.Write(@"c\MyCache\data.txt", "payload");
        var destination = Path.Combine(sb.TargetVolume, "MyCache");

        // 1) 先搬家
        var move = await new MigrationEngine().ExecuteAsync(Request(source, destination, MigrationMode.MoveAndLink));
        Assert.Equal(MigrationOutcome.Success, move.Outcome);

        // 2) 模拟"换了台机器 / 链接没了"：把原位置的链接拆掉
        LinkService.RemoveLink(source);
        Assert.False(Directory.Exists(source));

        // 3) 扫描目标盘，应该能凭来源标记把它找出来
        var found = await DirectoryScanner.ScanForOriginMarkersAsync(sb.TargetVolume, maxDepth: 4);

        var candidate = found.FirstOrDefault(f =>
            string.Equals(f.OriginalPath, PathGuard.Normalize(source), StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(candidate);
        Assert.False(candidate!.OriginAlreadyLinked);
        Assert.False(candidate.OriginOccupied);

        // 4) 接回原位
        var reconnect = await new MigrationEngine().ExecuteAsync(new MigrationRequest
        {
            SourcePath = candidate.OriginalPath!,
            DestinationPath = candidate.FullPath,
            Mode = MigrationMode.LinkOnly,
            Kind = MigrationJobKind.Reconnect,
        });

        Assert.Equal(MigrationOutcome.Success, reconnect.Outcome);
        Assert.Equal(LinkKind.Junction, ReparsePoint.GetLinkKind(source));
        Assert.True(LinkService.PointsTo(source, destination));

        // 接回来之后，原来的数据原封不动
        Assert.Equal("payload", File.ReadAllText(Path.Combine(source, "data.txt")));
    }
}
