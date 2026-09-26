using System.Security.Cryptography;
using Wyolm.Core.Interop;
using Wyolm.Core.Models;

namespace Wyolm.Core.Services;

/// <summary>校验进度。</summary>
public sealed record VerifyProgress(long Checked, long Total, string? Current);

/// <summary>
/// 复制结果校验器。
/// <para>
/// 这是"失败自动回滚"能成立的底气：只有校验明确通过，引擎才敢去动原目录。
/// 校验不通过就删掉目标副本、原目录一个字节都不碰。</para>
/// </summary>
public static class TreeVerifier
{
    /// <summary>时间戳比对容忍度。跨文件系统（如搬到 exFAT）会有 2 秒精度损失。</summary>
    private static readonly TimeSpan TimestampTolerance = TimeSpan.FromSeconds(2);

    /// <summary>最多报告多少条不一致，避免刷屏。</summary>
    public const int MaxReportedMismatches = 50;

    public static VerifyReport Verify(
        TreeManifest manifest,
        string destinationRoot,
        VerifyLevel level,
        CancellationToken ct,
        IProgress<VerifyProgress>? progress = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        destinationRoot = PathGuard.Normalize(destinationRoot);

        var mismatches = new List<VerifyMismatch>();
        long comparedFiles = 0, comparedBytes = 0, hashedFiles = 0;
        long total = manifest.Directories.Count + manifest.Files.Count + manifest.Links.Count;
        long checkedCount = 0;

        void Report(string? current) => progress?.Report(new VerifyProgress(checkedCount, total, current));

        void AddMismatch(string relative, string reason, long src, long dst)
        {
            if (mismatches.Count < MaxReportedMismatches)
                mismatches.Add(new VerifyMismatch(relative, reason, src, dst));
        }

        // ---- 目录 ----
        foreach (var dir in manifest.Directories)
        {
            ct.ThrowIfCancellationRequested();
            var target = Path.Combine(destinationRoot, dir.RelativePath);
            if (!Directory.Exists(target))
                AddMismatch(dir.RelativePath, "目标缺少这个目录", 1, 0);

            checkedCount++;
            if (checkedCount % 500 == 0) Report(dir.RelativePath);
        }

        // ---- 文件 ----
        foreach (var file in manifest.Files)
        {
            ct.ThrowIfCancellationRequested();
            var target = Path.Combine(destinationRoot, file.RelativePath);

            if (!File.Exists(target))
            {
                AddMismatch(file.RelativePath, "目标缺少这个文件", file.Length, 0);
                checkedCount++;
                continue;
            }

            FileInfo fi;
            try { fi = new FileInfo(target); }
            catch (Exception ex)
            {
                AddMismatch(file.RelativePath, $"读取目标文件失败：{ex.Message}", file.Length, 0);
                checkedCount++;
                continue;
            }

            if (fi.Length != file.Length)
            {
                AddMismatch(file.RelativePath, "文件大小不一致", file.Length, fi.Length);
                checkedCount++;
                continue;
            }

            if (level >= VerifyLevel.SizeAndTimestamp)
            {
                var delta = (fi.LastWriteTimeUtc - file.LastWriteUtc).Duration();
                if (delta > TimestampTolerance)
                {
                    AddMismatch(file.RelativePath, $"最后写入时间相差 {delta.TotalSeconds:0.#} 秒",
                        file.LastWriteUtc.Ticks, fi.LastWriteTimeUtc.Ticks);
                    checkedCount++;
                    continue;
                }
            }

            if (level >= VerifyLevel.ContentHash && file.Length > 0)
            {
                try
                {
                    var srcHash = HashFile(file.FullPath, ct);
                    var dstHash = HashFile(target, ct);
                    hashedFiles++;

                    if (!srcHash.AsSpan().SequenceEqual(dstHash))
                    {
                        AddMismatch(file.RelativePath, "SHA-256 内容哈希不一致", 0, 0);
                        checkedCount++;
                        continue;
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    AddMismatch(file.RelativePath, $"哈希计算失败：{ex.Message}", 0, 0);
                    checkedCount++;
                    continue;
                }
            }

            comparedFiles++;
            comparedBytes += file.Length;
            checkedCount++;

            if (checkedCount % 200 == 0) Report(file.RelativePath);
        }

        // ---- 嵌套链接 ----
        foreach (var link in manifest.Links)
        {
            ct.ThrowIfCancellationRequested();
            var target = Path.Combine(destinationRoot, link.RelativePath);
            var kind = ReparsePoint.GetLinkKind(target);
            if (kind == LinkKind.None)
                AddMismatch(link.RelativePath, "嵌套链接没有被重建", 1, 0);

            checkedCount++;
        }

        Report(null);
        sw.Stop();

        return new VerifyReport
        {
            Level = level,
            Mismatches = mismatches,
            ComparedFiles = comparedFiles,
            ComparedBytes = comparedBytes,
            HashedFiles = hashedFiles,
            Elapsed = sw.Elapsed,
        };
    }

    private static byte[] HashFile(string path, CancellationToken ct)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 1024 * 1024, FileOptions.SequentialScan);
        using var sha = SHA256.Create();

        var buffer = new byte[1024 * 1024];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            sha.TransformBlock(buffer, 0, read, null, 0);
        }

        sha.TransformFinalBlock([], 0, 0);
        return sha.Hash!;
    }

    /// <summary>把校验报告压成几行文字，用于日志和界面。</summary>
    public static IEnumerable<string> Describe(VerifyReport report)
    {
        yield return report.Summary;
        foreach (var m in report.Mismatches)
            yield return $"  · {m.RelativePath}：{m.Reason}（源 {m.SourceValue} / 目标 {m.DestinationValue}）";

        if (report.Mismatches.Count >= MaxReportedMismatches)
            yield return $"  · ……还有更多不一致未列出（已达 {MaxReportedMismatches} 条上限）";
    }
}
