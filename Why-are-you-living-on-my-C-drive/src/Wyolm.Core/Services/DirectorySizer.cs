using Wyolm.Core.Models;

namespace Wyolm.Core.Services;

/// <summary>一个目录的体积统计结果。</summary>
public sealed class SizeResult
{
    public long SizeBytes { get; set; }
    public long FileCount { get; set; }
    public long DirectoryCount { get; set; }
    public long InaccessibleCount { get; set; }
    public TimeSpan Elapsed { get; set; }
    public bool Cancelled { get; set; }
}

/// <summary>
/// 目录体积统计。
/// <para>
/// 刻意使用 <see cref="FileSystemEnumerable{T}"/> 的一次性遍历而不是
/// <c>Directory.GetFiles(..., AllDirectories)</c>：前者能拿到 <c>FileSystemEntry.Length</c>，
/// 不需要为每个文件额外发一次系统调用，在几十万个文件上能快好几倍。
/// </para>
/// <para>同时会跳过重解析点，避免顺着链接把同一个文件算两遍。</para>
/// </summary>
public static class DirectorySizer
{
    private static readonly EnumerationOptions DefaultOptions = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true,
        AttributesToSkip = 0,
        ReturnSpecialDirectories = false,
    };

    /// <summary>测量一个目录的真实体积。</summary>
    public static SizeResult Measure(string path, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new SizeResult();

        try
        {
            Walk(path, result, ct);
        }
        catch (OperationCanceledException)
        {
            result.Cancelled = true;
        }

        sw.Stop();
        result.Elapsed = sw.Elapsed;
        return result;
    }

    private static void Walk(string path, SizeResult result, CancellationToken ct)
    {
        var stack = new Stack<string>();
        stack.Push(path);

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var current = stack.Pop();

            IEnumerable<FileSystemInfo> entries;
            try
            {
                entries = new DirectoryInfo(current)
                    .EnumerateFileSystemInfos("*", DefaultOptions);
            }
            catch (UnauthorizedAccessException)
            {
                result.InaccessibleCount++;
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }
            catch (IOException)
            {
                result.InaccessibleCount++;
                continue;
            }

            using var e = entries.GetEnumerator();
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                FileSystemInfo entry;
                try
                {
                    if (!e.MoveNext()) break;
                    entry = e.Current;
                }
                catch (UnauthorizedAccessException) { result.InaccessibleCount++; continue; }
                catch (DirectoryNotFoundException) { break; }
                catch (IOException) { result.InaccessibleCount++; continue; }

                // 跳过链接本身：不要把链接指向的内容重复计入。
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    continue;

                if ((entry.Attributes & FileAttributes.Directory) != 0)
                {
                    result.DirectoryCount++;
                    stack.Push(entry.FullName);
                }
                else
                {
                    result.FileCount++;
                    try
                    {
                        if (entry is FileInfo fi) result.SizeBytes += fi.Length;
                    }
                    catch (IOException) { result.InaccessibleCount++; }
                }
            }
        }
    }

    /// <summary>测量目录，并定期回调进度（用于体积巨大的目录）。</summary>
    public static SizeResult MeasureWithProgress(string path, IProgress<long>? filesSeen, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new SizeResult();
        try
        {
            Walk(path, result, ct, filesSeen);
        }
        catch (OperationCanceledException)
        {
            result.Cancelled = true;
        }
        sw.Stop();
        result.Elapsed = sw.Elapsed;
        return result;
    }

    private static void Walk(string path, SizeResult result, CancellationToken ct, IProgress<long>? filesSeen)
    {
        var stack = new Stack<string>();
        stack.Push(path);
        long lastReport = 0;

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var current = stack.Pop();

            DirectoryInfo di;
            FileSystemInfo[] batch;
            try
            {
                di = new DirectoryInfo(current);
                batch = di.GetFileSystemInfos("*", DefaultOptions);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
            {
                result.InaccessibleCount++;
                continue;
            }

            foreach (var entry in batch)
            {
                ct.ThrowIfCancellationRequested();
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) continue;

                if ((entry.Attributes & FileAttributes.Directory) != 0)
                {
                    result.DirectoryCount++;
                    stack.Push(entry.FullName);
                }
                else
                {
                    result.FileCount++;
                    try { if (entry is FileInfo fi) result.SizeBytes += fi.Length; }
                    catch (IOException) { result.InaccessibleCount++; }

                    if (filesSeen is not null && result.FileCount - lastReport >= 2000)
                    {
                        lastReport = result.FileCount;
                        filesSeen.Report(result.FileCount);
                    }
                }
            }
        }

        filesSeen?.Report(result.FileCount);
    }
}
