using Wyolm.Core.Interop;
using Wyolm.Core.Models;

namespace Wyolm.Core.Services;

/// <summary>清单里的一条记录。</summary>
public sealed class TreeEntry
{
    public required string RelativePath { get; init; }
    public required string FullPath { get; init; }
    public long Length { get; init; }
    public DateTime LastWriteUtc { get; init; }
    public DateTime CreationUtc { get; init; }
    public FileAttributes Attributes { get; init; }
    public LinkKind LinkKind { get; init; } = LinkKind.None;
    public string? LinkTarget { get; init; }
}

/// <summary>
/// 一个目录树的完整清单。
/// <para>
/// 只枚举一次，然后复制和校验都基于它。这样有三个好处：
/// 进度条能提前知道总量；校验不需要再遍历一遍源目录；
/// 复制过程中源目录被改动也不会影响"应该复制什么"的判断。</para>
/// </summary>
public sealed class TreeManifest
{
    public required string RootPath { get; init; }
    public required IReadOnlyList<TreeEntry> Directories { get; init; }
    public required IReadOnlyList<TreeEntry> Files { get; init; }

    /// <summary>源目录里嵌套的链接。不会跟随，而是原样重建。</summary>
    public required IReadOnlyList<TreeEntry> Links { get; init; }

    public long TotalBytes { get; init; }

    /// <summary>枚举时因为权限 / 占用读不到的条目，会上报给用户。</summary>
    public IReadOnlyList<string> Inaccessible { get; init; } = [];

    public bool FullyAccessible => Inaccessible.Count == 0;

    public long TotalEntries => Files.Count + Directories.Count + Links.Count;
}

/// <summary>把一个目录树枚举成清单。</summary>
public static class TreeScanner
{
    private static readonly EnumerationOptions Options = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true,
        AttributesToSkip = 0,
        ReturnSpecialDirectories = false,
    };

    public static TreeManifest Build(string rootPath, CancellationToken ct,
        IProgress<(long Files, long Bytes, string? Current)>? progress = null)
    {
        var root = PathGuard.Normalize(rootPath);
        var directories = new List<TreeEntry>();
        var files = new List<TreeEntry>();
        var links = new List<TreeEntry>();
        var inaccessible = new List<string>();
        long totalBytes = 0;

        var stack = new Stack<(string Full, string Relative)>();
        stack.Push((root, string.Empty));

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var (current, relativeBase) = stack.Pop();

            IEnumerable<FileSystemInfo> entries;
            try
            {
                entries = new DirectoryInfo(current).EnumerateFileSystemInfos("*", Options);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
            {
                inaccessible.Add(current);
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
                catch (UnauthorizedAccessException) { continue; }
                catch (DirectoryNotFoundException) { break; }
                catch (IOException) { continue; }

                var relative = string.IsNullOrEmpty(relativeBase)
                    ? entry.Name
                    : Path.Combine(relativeBase, entry.Name);

                bool isReparse = (entry.Attributes & FileAttributes.ReparsePoint) != 0;

                if (isReparse)
                {
                    // 嵌套链接：记录目标，复制时原样重建，绝不跟随。
                    var kind = ReparsePoint.GetLinkKind(entry.FullName);
                    links.Add(new TreeEntry
                    {
                        RelativePath = relative,
                        FullPath = entry.FullName,
                        LinkKind = kind == LinkKind.None ? LinkKind.Junction : kind,
                        LinkTarget = ReparsePoint.ReadLinkTarget(entry.FullName),
                        Attributes = entry.Attributes,
                    });
                    continue;
                }

                if ((entry.Attributes & FileAttributes.Directory) != 0)
                {
                    directories.Add(new TreeEntry
                    {
                        RelativePath = relative,
                        FullPath = entry.FullName,
                        LastWriteUtc = entry.LastWriteTimeUtc,
                        CreationUtc = entry.CreationTimeUtc,
                        Attributes = entry.Attributes,
                    });
                    stack.Push((entry.FullName, relative));
                }
                else
                {
                    long length = 0;
                    try { if (entry is FileInfo fi) length = fi.Length; }
                    catch (IOException) { inaccessible.Add(entry.FullName); }

                    files.Add(new TreeEntry
                    {
                        RelativePath = relative,
                        FullPath = entry.FullName,
                        Length = length,
                        LastWriteUtc = entry.LastWriteTimeUtc,
                        CreationUtc = entry.CreationTimeUtc,
                        Attributes = entry.Attributes,
                    });

                    totalBytes += length;

                    if (progress is not null && files.Count % 500 == 0)
                        progress.Report((files.Count, totalBytes, relative));
                }
            }
        }

        progress?.Report((files.Count, totalBytes, null));

        return new TreeManifest
        {
            RootPath = root,
            Directories = directories,
            Files = files,
            Links = links,
            TotalBytes = totalBytes,
            Inaccessible = inaccessible,
        };
    }
}
