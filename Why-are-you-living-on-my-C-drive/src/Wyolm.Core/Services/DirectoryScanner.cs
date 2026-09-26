using Wyolm.Core.Interop;
using Wyolm.Core.Models;

namespace Wyolm.Core.Services;

/// <summary>扫描进度。</summary>
public sealed record ScanProgress(string Stage, string? Current, int Done, int Total);

/// <summary>一次扫描的结果。</summary>
public sealed class ScanResult
{
    public required string RootPath { get; init; }
    public required IReadOnlyList<ScanItem> Items { get; init; }
    public long TotalBytes { get; init; }
    public long TotalFiles { get; init; }
    public TimeSpan Elapsed { get; init; }
    public bool Cancelled { get; init; }
    public int SkippedBecauseInaccessible { get; init; }
}

/// <summary>
/// 盘符扫描器。
/// <para>两种模式：列出某个目录下第一层的候选文件夹（带体积），
/// 以及在整个盘里翻找本工具留下的来源标记，用于"接回原位"。</para>
/// </summary>
public static class DirectoryScanner
{
    /// <summary>名字命中就给个"值得搬"提示的目录。</summary>
    private static readonly (string Name, string Hint)[] KnownHeavyNames =
    [
        (".nuget", "NuGet 包缓存"),
        (".m2", "Maven 本地仓库"),
        (".gradle", "Gradle 缓存"),
        (".cargo", "Cargo 注册表缓存"),
        (".conda", "Conda 环境与包缓存"),
        (".cache", "通用缓存目录"),
        (".npm", "npm 缓存"),
        (".pnpm-store", "pnpm 内容寻址仓库"),
        (".yarn", "Yarn 缓存"),
        (".docker", "Docker CLI 配置与镜像缓存"),
        (".ollama", "Ollama 模型"),
        (".vscode", "VS Code 扩展与缓存"),
        (".android", "Android SDK / 模拟器镜像"),
        (".gradle-cache", "Gradle 缓存"),
        ("node_modules", "Node 依赖（通常可以整个删掉重装）"),
        ("packages", "Visual Studio / NuGet 包目录"),
        ("Temp", "临时文件"),
        ("WindowsApps", "应用商店应用数据（不建议直接搬）"),
        ("CrashDumps", "崩溃转储"),
        ("Cache", "缓存"),
        ("CacheStorage", "浏览器缓存"),
        ("Code Cache", "浏览器代码缓存"),
        ("GPUCache", "GPU 着色器缓存"),
        ("Docker", "Docker 数据"),
        ("WSL", "WSL 发行版磁盘"),
        ("OneDriveTemp", "OneDrive 临时缓存"),
    ];

    /// <summary>路径片段命中就给提示的（用于 AppData 里的深层目录）。</summary>
    private static readonly (string Fragment, string Hint)[] KnownHeavyFragments =
    [
        (@"\AppData\Local\Temp", "系统临时目录"),
        (@"\AppData\Local\Packages", "UWP 应用数据"),
        (@"\AppData\Local\Google\Chrome\User Data", "Chrome 用户数据"),
        (@"\AppData\Local\Microsoft\Edge\User Data", "Edge 用户数据"),
        (@"\AppData\Local\JetBrains", "JetBrains IDE 缓存与索引"),
        (@"\AppData\Local\Programs", "用户级安装的程序"),
        (@"\AppData\Roaming\Code", "VS Code 用户数据"),
        (@"\AppData\Local\pip\Cache", "pip 下载缓存"),
        (@"\AppData\Local\NVIDIA", "NVIDIA 着色器缓存"),
        (@"\AppData\Local\AMD", "AMD 着色器缓存"),
        (@"\AppData\Local\D3DSCache", "DirectX 着色器缓存"),
        (@"\AppData\Local\CrashDumps", "崩溃转储"),
        (@"\.vscode\extensions", "VS Code 扩展"),
        (@"\docker\wsl", "Docker Desktop WSL 数据"),
    ];

    /// <summary>扫描时不去碰的系统目录名。</summary>
    private static readonly HashSet<string> SkipDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "$Recycle.Bin", "System Volume Information", "Windows", "Program Files", "Program Files (x86)",
        "ProgramData", "Recovery", "Boot", "$WinREAgent", "Config.Msi", "MSOCache",
        "node_modules", "obj", "bin", ".git",
    };

    /// <summary>
    /// 列出 <paramref name="root"/> 下第一层的候选文件夹并统计体积。
    /// </summary>
    public static async Task<ScanResult> ScanChildrenAsync(
        string root,
        bool includeHidden = false,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        root = PathGuard.Normalize(root);

        var children = new List<DirectoryInfo>();
        try
        {
            var di = new DirectoryInfo(root);
            foreach (var child in di.EnumerateDirectories("*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                AttributesToSkip = 0,
                ReturnSpecialDirectories = false,
            }))
            {
                if (!includeHidden && (child.Attributes & FileAttributes.Hidden) != 0)
                    continue;
                if (!includeHidden && (child.Attributes & FileAttributes.System) != 0)
                    continue;
                children.Add(child);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
        {
            return new ScanResult { RootPath = root, Items = [], Elapsed = sw.Elapsed };
        }

        var items = new ScanItem[children.Count];
        long totalBytes = 0, totalFiles = 0;
        int done = 0, skipped = 0;

        // 体积统计是 IO 密集的，按 CPU 核数并行；每个目录之间互相独立。
        int parallelism = Math.Clamp(Environment.ProcessorCount, 2, 8);
        using var gate = new SemaphoreSlim(parallelism);

        var tasks = new List<Task>(children.Count);
        var sync = new object();

        for (int i = 0; i < children.Count; i++)
        {
            int index = i;
            var child = children[index];

            await gate.WaitAsync(ct).ConfigureAwait(false);

            tasks.Add(Task.Run(() =>
            {
                try
                {
                    ct.ThrowIfCancellationRequested();

                    var childPath = child.FullName;
                    var linkKind = ReparsePoint.GetLinkKind(childPath);
                    var linkTarget = linkKind == LinkKind.None ? null : ReparsePoint.ReadLinkTarget(childPath);

                    SizeResult size = linkKind == LinkKind.None
                        ? DirectorySizer.Measure(childPath, ct)
                        : new SizeResult();

                    var item = new ScanItem
                    {
                        FullPath = childPath,
                        Name = child.Name,
                        RootPath = root,
                        SizeBytes = size.SizeBytes,
                        FileCount = size.FileCount,
                        DirectoryCount = size.DirectoryCount,
                        InaccessibleCount = size.InaccessibleCount,
                        LinkKind = linkKind,
                        LinkTarget = linkTarget,
                    };

                    ApplyHints(item);

                    items[index] = item;

                    lock (sync)
                    {
                        totalBytes += item.SizeBytes;
                        totalFiles += item.FileCount;
                        if (size.InaccessibleCount > 0) skipped++;
                        done++;
                        progress?.Report(new ScanProgress("统计体积", item.Name, done, children.Count));
                    }
                }
                finally
                {
                    gate.Release();
                }
            }, ct));
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new ScanResult
            {
                RootPath = root,
                Items = items.Where(x => x is not null).ToList(),
                Elapsed = sw.Elapsed,
                Cancelled = true,
            };
        }

        sw.Stop();
        return new ScanResult
        {
            RootPath = root,
            Items = items.Where(x => x is not null).OrderByDescending(x => x.SizeBytes).ToList(),
            TotalBytes = totalBytes,
            TotalFiles = totalFiles,
            Elapsed = sw.Elapsed,
            SkippedBecauseInaccessible = skipped,
        };
    }

    /// <summary>
    /// 在整个盘里递归翻找来源标记，找出"曾经被搬走、现在可以接回原位"的目录。
    /// </summary>
    /// <param name="root">要翻找的盘或目录。</param>
    /// <param name="maxDepth">最大下钻层数，防止在超大目录上跑太久。</param>
    public static Task<IReadOnlyList<ScanItem>> ScanForOriginMarkersAsync(
        string root,
        int maxDepth = 6,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
        => Task.Run(() => ScanForOriginMarkers(root, maxDepth, progress, ct), ct);

    private static IReadOnlyList<ScanItem> ScanForOriginMarkers(
        string root, int maxDepth, IProgress<ScanProgress>? progress, CancellationToken ct)
    {
        root = PathGuard.Normalize(root);
        var found = new List<ScanItem>();
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((root, 0));

        int visited = 0;

        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var (current, depth) = queue.Dequeue();
            visited++;

            if (visited % 25 == 0)
                progress?.Report(new ScanProgress("查找来源标记", current, visited, found.Count));

            DirectoryInfo di;
            DirectoryInfo[] subdirs;
            try
            {
                di = new DirectoryInfo(current);
                subdirs = di.GetDirectories("*", new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    AttributesToSkip = 0,
                    ReturnSpecialDirectories = false,
                });
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
            {
                continue;
            }

            foreach (var sub in subdirs)
            {
                ct.ThrowIfCancellationRequested();

                // 不跟随链接，否则会顺着接回来的链接又跑回 C 盘。
                if ((sub.Attributes & FileAttributes.ReparsePoint) != 0) continue;

                var marker = OriginMarkerStore.Read(sub.FullName);
                if (marker is not null && !string.IsNullOrWhiteSpace(marker.OriginalPath))
                {
                    var (originKind, originTarget) = LinkService.Inspect(marker.OriginalPath);

                    var alreadyLinked = originKind != LinkKind.None &&
                                        originTarget is not null &&
                                        string.Equals(originTarget.TrimEnd('\\'),
                                            sub.FullName.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

                    var occupied = originKind == LinkKind.None && Directory.Exists(marker.OriginalPath) &&
                                   !ReparsePoint.IsDirectoryEmpty(marker.OriginalPath);

                    found.Add(new ScanItem
                    {
                        FullPath = sub.FullName,
                        Name = sub.Name,
                        RootPath = root,
                        LinkKind = LinkKind.None,
                        HasOriginMarker = true,
                        OriginalPath = marker.OriginalPath,
                        OriginAlreadyLinked = alreadyLinked,
                        OriginOccupied = occupied,
                        Suggestion = alreadyLinked
                            ? "已接好"
                            : occupied
                                ? "原位置被占用，需先清空"
                                : originKind != LinkKind.None
                                    ? "原位置被别的链接占着"
                                    : "可以接回",
                    });
                }

                if (depth + 1 >= maxDepth) continue;
                if (SkipDirectoryNames.Contains(sub.Name)) continue;

                // 跳过明显是应用数据的超深目录，避免把整个盘翻穿。
                queue.Enqueue((sub.FullName, depth + 1));
            }
        }

        progress?.Report(new ScanProgress("查找来源标记", null, visited, found.Count));
        return found;
    }

    private static void ApplyHints(ScanItem item)
    {
        foreach (var (name, hint) in KnownHeavyNames)
        {
            if (string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                item.IsKnownHeavyPath = true;
                item.Suggestion = hint;
                return;
            }
        }

        foreach (var (fragment, hint) in KnownHeavyFragments)
        {
            if (item.FullPath.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                item.IsKnownHeavyPath = true;
                item.Suggestion = hint;
                return;
            }
        }
    }
}
