using Wyolm.Core.Models;
using Wyolm.Core.Services;

namespace Wyolm.Core.Tests;

/// <summary>清单 / 复制 / 校验三件套。</summary>
public sealed class TreeCopyVerifyTests
{
    private static TestSandbox BuildTree(out string source)
    {
        var sb = new TestSandbox();
        source = sb.Dir(@"c\Source");

        sb.Write(@"c\Source\a.txt", "aaa");
        sb.Write(@"c\Source\sub\b.txt", "bbbb");
        sb.Write(@"c\Source\sub\deeper\c.txt", new string('c', 5000));
        sb.Write(@"c\Source\空目录占位\.keep", string.Empty);
        sb.Write(@"c\Source\中文目录\中文文件.txt", "中文内容");
        // 两个不同目录下的同名文件
        sb.Write(@"c\Source\x\same.txt", "one");
        sb.Write(@"c\Source\y\same.txt", "two");

        return sb;
    }

    [Fact]
    public void Manifest_captures_everything()
    {
        using var sb = BuildTree(out var source);

        var manifest = TreeScanner.Build(source, CancellationToken.None);

        Assert.Equal(7, manifest.Files.Count);
        Assert.True(manifest.Directories.Count >= 5);
        Assert.True(manifest.FullyAccessible);

        // 逐条核对长度：中文字符是多字节、空文件是 0 字节、
        // 同名文件在不同目录下都要被单独记录。
        Assert.Equal(3, manifest.Files.Single(f => f.RelativePath == "a.txt").Length);
        Assert.Equal(5000, manifest.Files.Single(f => f.RelativePath == Path.Combine("sub", "deeper", "c.txt")).Length);
        Assert.Equal(0, manifest.Files.Single(f => f.RelativePath.EndsWith(".keep", StringComparison.Ordinal)).Length);
        // "中文内容" 是 4 个字符 × 3 字节
        Assert.Equal(12, manifest.Files.Single(f => f.RelativePath.EndsWith("中文文件.txt", StringComparison.Ordinal)).Length);
        Assert.Equal(manifest.Files.Sum(f => f.Length), manifest.TotalBytes);
        Assert.Equal(
            new[] { "one", "two" },
            manifest.Files.Where(f => f.RelativePath.EndsWith("same.txt", StringComparison.Ordinal))
                .Select(f => File.ReadAllText(f.FullPath)).OrderBy(x => x).ToArray());
    }

    [Fact]
    public void Copy_then_full_hash_verify_passes()
    {
        using var sb = BuildTree(out var source);
        var destination = Path.Combine(sb.TargetVolume, "Copy");

        var manifest = TreeScanner.Build(source, CancellationToken.None);
        FileTreeCopier.Copy(manifest, destination, preserveAttributes: true, allowMerge: false,
            CancellationToken.None);

        var report = TreeVerifier.Verify(manifest, destination, VerifyLevel.ContentHash, CancellationToken.None);

        Assert.True(report.Passed, report.Summary);
        Assert.Equal(manifest.Files.Count, report.ComparedFiles);
        // 零字节文件不做哈希（算了也没意义），所以哈希计数要排除它们
        Assert.Equal(manifest.Files.Count(f => f.Length > 0), report.HashedFiles);

        // 内容真的到了
        Assert.Equal("中文内容", File.ReadAllText(Path.Combine(destination, "中文目录", "中文文件.txt")));
        Assert.Equal("one", File.ReadAllText(Path.Combine(destination, "x", "same.txt")));
        Assert.Equal("two", File.ReadAllText(Path.Combine(destination, "y", "same.txt")));
    }

    /// <summary>
    /// 证明"全量哈希"这一档不是多余的：把内容换成等长的另一种内容并复原时间戳，
    /// 结构和大小对得上，只有哈希能识破。
    /// </summary>
    [Fact]
    public void Hash_level_catches_a_same_length_content_swap_that_lighter_levels_miss()
    {
        using var sb = new TestSandbox();
        var source = sb.Dir(@"c\Source");
        sb.Write(@"c\Source\a.txt", "abcdefghij");
        var destination = Path.Combine(sb.TargetVolume, "Copy");

        var manifest = TreeScanner.Build(source, CancellationToken.None);
        FileTreeCopier.Copy(manifest, destination, true, false, CancellationToken.None);

        var targetFile = Path.Combine(destination, "a.txt");
        var originalTimestamp = File.GetLastWriteTimeUtc(targetFile);

        // 等长换内容，再把时间戳改回去
        File.WriteAllText(targetFile, "ABCDEFGHIJ");
        File.SetLastWriteTimeUtc(targetFile, originalTimestamp);

        var structure = TreeVerifier.Verify(manifest, destination, VerifyLevel.Structure, CancellationToken.None);
        var sizeAndTime = TreeVerifier.Verify(manifest, destination, VerifyLevel.SizeAndTimestamp, CancellationToken.None);
        var hash = TreeVerifier.Verify(manifest, destination, VerifyLevel.ContentHash, CancellationToken.None);

        Assert.True(structure.Passed, "只看结构应该发现不了等长替换");
        Assert.True(sizeAndTime.Passed, "只看大小和时间戳也应该发现不了（时间戳已被改回）");
        Assert.False(hash.Passed, "全量哈希必须发现内容被换过");
        Assert.Contains(hash.Mismatches, m => m.Reason.Contains("哈希"));
    }

    [Fact]
    public void Missing_file_at_destination_is_detected()
    {
        using var sb = BuildTree(out var source);
        var destination = Path.Combine(sb.TargetVolume, "Copy");

        var manifest = TreeScanner.Build(source, CancellationToken.None);
        FileTreeCopier.Copy(manifest, destination, true, false, CancellationToken.None);

        File.Delete(Path.Combine(destination, "sub", "b.txt"));

        var report = TreeVerifier.Verify(manifest, destination, VerifyLevel.SizeAndTimestamp, CancellationToken.None);

        Assert.False(report.Passed);
        Assert.Contains(report.Mismatches, m => m.RelativePath == Path.Combine("sub", "b.txt"));
    }

    [Fact]
    public void Copying_into_a_non_empty_destination_without_merge_is_refused()
    {
        using var sb = BuildTree(out var source);
        var destination = sb.Dir(@"d\Copy");
        sb.Write(@"d\Copy\a.txt", "already here");

        var manifest = TreeScanner.Build(source, CancellationToken.None);

        Assert.Throws<IOException>(() =>
            FileTreeCopier.Copy(manifest, destination, true, allowMerge: false, CancellationToken.None));
    }

    [Fact]
    public void Merge_mode_records_exactly_which_files_it_created()
    {
        using var sb = new TestSandbox();
        var source = sb.Dir(@"c\Source");
        sb.Write(@"c\Source\new1.txt", "n1");
        sb.Write(@"c\Source\new2.txt", "n2");

        var destination = sb.Dir(@"d\Copy");
        var preexisting = sb.Write(@"d\Copy\user-file.txt", "user data");

        var manifest = TreeScanner.Build(source, CancellationToken.None);
        var created = new List<string>();

        FileTreeCopier.Copy(manifest, destination, true, allowMerge: true, CancellationToken.None,
            progress: null, log: null, createdFiles: created);

        // 只记录我们写进去的，绝不包含用户原有的文件
        Assert.Equal(2, created.Count);
        Assert.Contains(created, c => c.EndsWith("new1.txt", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(created, c => c.EndsWith("new2.txt", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(created, c => c.EndsWith("user-file.txt", StringComparison.OrdinalIgnoreCase));

        Assert.True(File.Exists(preexisting));

        var report = TreeVerifier.Verify(manifest, destination, VerifyLevel.ContentHash, CancellationToken.None);
        Assert.True(report.Passed, report.Summary);
    }

    [Fact]
    public void Copy_preserves_timestamps_and_readonly_attribute()
    {
        using var sb = new TestSandbox();
        var source = sb.Dir(@"c\Source");
        var file = sb.Write(@"c\Source\ro.txt", "read only");

        var stamp = new DateTime(2020, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(file, stamp);
        File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.ReadOnly);

        var destination = Path.Combine(sb.TargetVolume, "Copy");
        var manifest = TreeScanner.Build(source, CancellationToken.None);
        FileTreeCopier.Copy(manifest, destination, preserveAttributes: true, allowMerge: false,
            CancellationToken.None);

        var copied = Path.Combine(destination, "ro.txt");
        Assert.True(File.Exists(copied));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(copied));
        Assert.True((File.GetAttributes(copied) & FileAttributes.ReadOnly) != 0);

        var report = TreeVerifier.Verify(manifest, destination, VerifyLevel.SizeAndTimestamp, CancellationToken.None);
        Assert.True(report.Passed, report.Summary);

        // 收尾：只读文件会让沙箱清理失败，先去掉属性
        File.SetAttributes(file, FileAttributes.Normal);
        File.SetAttributes(copied, FileAttributes.Normal);
    }

    [Fact]
    public void Nested_links_are_rebuilt_not_followed()
    {
        using var sb = new TestSandbox();

        var outside = sb.Dir(@"d\Outside");
        sb.Write(@"d\Outside\outside-file.txt", "outside data");

        var source = sb.Dir(@"c\Source");
        sb.Write(@"c\Source\real.txt", "real");
        Interop.ReparsePoint.CreateJunction(Path.Combine(source, "shortcut"), outside);

        var destination = Path.Combine(sb.TargetVolume, "Copy");
        var manifest = TreeScanner.Build(source, CancellationToken.None);

        Assert.Single(manifest.Links);

        FileTreeCopier.Copy(manifest, destination, true, false, CancellationToken.None);

        // 目标里的快捷方式必须还是一个链接，而不是一份拷贝
        var copiedLink = Path.Combine(destination, "shortcut");
        Assert.Equal(LinkKind.Junction, Interop.ReparsePoint.GetLinkKind(copiedLink));
        Assert.True(LinkService.PointsTo(copiedLink, outside));

        var report = TreeVerifier.Verify(manifest, destination, VerifyLevel.ContentHash, CancellationToken.None);
        Assert.True(report.Passed, report.Summary);

        // 证明是"同一份数据"而不是复制品：透过链接写进去，outside 里立刻就能看到
        File.WriteAllText(Path.Combine(copiedLink, "written-through-link.txt"), "shared");
        Assert.True(File.Exists(Path.Combine(outside, "written-through-link.txt")));
        Assert.Equal("shared", File.ReadAllText(Path.Combine(outside, "written-through-link.txt")));
    }
}
