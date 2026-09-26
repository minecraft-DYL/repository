using Wyolm.Core.Interop;
using Wyolm.Core.Models;

namespace Wyolm.Core.Services;

/// <summary>复制进度。</summary>
public sealed record CopyProgress(long CopiedFiles, long TotalFiles, long CopiedBytes, long TotalBytes, string? CurrentFile);

/// <summary>
/// 目录树复制器。
/// <para>要点：保留时间戳与属性；对大于 64MB 的文件分段复制以便进度条能动；
/// 遇到嵌套链接原样重建而不是跟随；任何异常都向上抛，由引擎统一回滚。</para>
/// </summary>
public static class FileTreeCopier
{
    private const int BufferSize = 4 * 1024 * 1024;      // 4MB 缓冲
    private const long ChunkedThreshold = 64L * 1024 * 1024; // 超过这个大小走分段复制

    /// <summary>
    /// 把 <paramref name="manifest"/> 描述的内容复制到 <paramref name="destinationRoot"/>。
    /// 调用前请确保目标目录不存在或为空（合并模式除外）。
    /// </summary>
    public static void Copy(
        TreeManifest manifest,
        string destinationRoot,
        bool preserveAttributes,
        bool allowMerge,
        CancellationToken ct,
        IProgress<CopyProgress>? progress = null,
        Action<string>? log = null,
        ICollection<string>? createdFiles = null)
    {
        destinationRoot = PathGuard.Normalize(destinationRoot);
        Directory.CreateDirectory(destinationRoot);

        long copiedFiles = 0, copiedBytes = 0;

        // ---- 目录：按深度从浅到深创建，保证父目录先存在 ----
        foreach (var dir in manifest.Directories.OrderBy(d => d.RelativePath.Count(c => c == '\\')))
        {
            ct.ThrowIfCancellationRequested();
            var target = Path.Combine(destinationRoot, dir.RelativePath);
            Directory.CreateDirectory(target);
        }

        // ---- 文件 ----
        foreach (var file in manifest.Files)
        {
            ct.ThrowIfCancellationRequested();
            var target = Path.Combine(destinationRoot, file.RelativePath);

            var parent = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

            if (File.Exists(target))
            {
                if (!allowMerge)
                    throw new IOException($"目标位置已存在同名文件：{file.RelativePath}");

                // 只读文件必须先去掉只读属性才能覆盖。
                try
                {
                    var existing = File.GetAttributes(target);
                    if ((existing & FileAttributes.ReadOnly) != 0)
                        File.SetAttributes(target, existing & ~FileAttributes.ReadOnly);
                }
                catch (Exception) { }
            }

            CopyOneFile(file, target, ct);
            createdFiles?.Add(target);

            copiedFiles++;
            copiedBytes += file.Length;

            if (preserveAttributes) ApplyMetadata(file, target);

            if (progress is not null && (copiedFiles % 50 == 0 || copiedBytes == manifest.TotalBytes))
                progress.Report(new CopyProgress(copiedFiles, manifest.Files.Count, copiedBytes, manifest.TotalBytes, file.RelativePath));
        }

        // ---- 目录时间戳：从深到浅设置，避免给子目录设时间时父目录时间又被改 ----
        if (preserveAttributes)
        {
            foreach (var dir in manifest.Directories.OrderByDescending(d => d.RelativePath.Count(c => c == '\\')))
            {
                try
                {
                    var target = Path.Combine(destinationRoot, dir.RelativePath);
                    if (Directory.Exists(target))
                        Directory.SetLastWriteTimeUtc(target, dir.LastWriteUtc);
                }
                catch (Exception) { }
            }
        }

        // ---- 嵌套链接：原样重建 ----
        foreach (var link in manifest.Links)
        {
            ct.ThrowIfCancellationRequested();
            var target = Path.Combine(destinationRoot, link.RelativePath);
            var parent = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

            if (link.LinkTarget is null)
            {
                log?.Invoke($"跳过无法解析的链接：{link.RelativePath}");
                continue;
            }

            var resolvedTarget = link.LinkTarget;

            // 链接指向源目录内部时，改指向目标里的对应位置。
            if (PathGuard.IsSameOrUnder(resolvedTarget, manifest.RootPath))
            {
                var rel = Path.GetRelativePath(manifest.RootPath, resolvedTarget);
                resolvedTarget = Path.Combine(destinationRoot, rel);
            }

            if (!Directory.Exists(resolvedTarget) && !File.Exists(resolvedTarget))
            {
                log?.Invoke($"嵌套链接的目标不存在，已跳过：{link.RelativePath} -> {link.LinkTarget}");
                continue;
            }

            if (Directory.Exists(target) || File.Exists(target))
            {
                log?.Invoke($"嵌套链接位置已被占用，已跳过：{link.RelativePath}");
                continue;
            }

            try
            {
                if (Directory.Exists(resolvedTarget))
                    ReparsePoint.CreateJunction(target, resolvedTarget);
                else
                    ReparsePoint.CreateSymbolicLink(target, resolvedTarget);
                log?.Invoke($"重建了嵌套链接：{link.RelativePath}");
            }
            catch (Exception ex)
            {
                log?.Invoke($"重建嵌套链接失败（已跳过）：{link.RelativePath}：{ex.Message}");
            }
        }

        progress?.Report(new CopyProgress(copiedFiles, manifest.Files.Count, copiedBytes, manifest.TotalBytes, null));
    }

    /// <summary>复制单个文件。大文件分段复制，让小文件也能得到及时反馈。</summary>
    private static void CopyOneFile(TreeEntry file, string target, CancellationToken ct)
    {
        // 先清掉目标上可能存在的只读文件
        if (File.Exists(target))
        {
            try
            {
                var attrs = File.GetAttributes(target);
                if ((attrs & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(target, attrs & ~FileAttributes.ReadOnly);
            }
            catch (Exception) { }
            File.Delete(target);
        }

        if (file.Length < ChunkedThreshold)
        {
            File.Copy(file.FullPath, target, overwrite: true);
            return;
        }

        using var source = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, BufferSize, FileOptions.SequentialScan);
        using var dest = new FileStream(target, FileMode.Create, FileAccess.Write,
            FileShare.None, BufferSize, FileOptions.SequentialScan);

        var buffer = new byte[BufferSize];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            dest.Write(buffer, 0, read);
        }

        dest.Flush(flushToDisk: true);
    }

    /// <summary>把源文件的时间戳和属性搬到目标文件上。</summary>
    private static void ApplyMetadata(TreeEntry source, string target)
    {
        try
        {
            File.SetLastWriteTimeUtc(target, source.LastWriteUtc);
            File.SetCreationTimeUtc(target, source.CreationUtc);
        }
        catch (Exception)
        {
            // 时间戳设不上不影响数据正确性
        }

        try
        {
            // 只保留可安全复制的属性位，避免把 ReparsePoint / Offline 之类带过去。
            var safe = source.Attributes &
                       (FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System | FileAttributes.Archive);
            if (safe != 0)
                File.SetAttributes(target, safe);
        }
        catch (Exception) { }
    }
}
