using System.Diagnostics;

namespace Wyolm.Core.Services;

/// <summary>占用探测的结果。</summary>
public sealed class LockReport
{
    /// <summary>可执行文件位于待搬目录内的进程 —— 这些几乎一定会导致搬家失败。</summary>
    public required IReadOnlyList<ProcessRef> ProcessesFromPath { get; init; }

    /// <summary>尝试独占打开失败的样本文件 —— 大概率被别的程序占用。</summary>
    public required IReadOnlyList<string> LockedSamples { get; init; }

    /// <summary>总共探测了多少个文件。</summary>
    public int ProbedFiles { get; init; }

    public bool HasBlockers => ProcessesFromPath.Count > 0;

    public string Summary
    {
        get
        {
            if (ProcessesFromPath.Count > 0)
                return $"发现 {ProcessesFromPath.Count} 个正在从该目录运行的进程：" +
                       string.Join("、", ProcessesFromPath.Take(5).Select(p => $"{p.Name}(PID {p.Pid})"));

            if (LockedSamples.Count > 0)
                return $"有 {LockedSamples.Count} 个样本文件正被占用（共探测 {ProbedFiles} 个）。";

            return $"未发现明显占用（探测了 {ProbedFiles} 个文件）。";
        }
    }
}

public sealed record ProcessRef(int Pid, string Name, string? Path);

/// <summary>
/// 占用探测。
/// <para>搬家最怕的就是有程序正开着源目录里的文件：复制可能读到半截内容，
/// 改名会直接失败。这个类做一个廉价的预检，把风险提前告诉用户。</para>
/// </summary>
public static class LockDetector
{
    /// <summary>找出可执行文件位于指定目录下的进程。</summary>
    public static IReadOnlyList<ProcessRef> FindProcessesFromPath(string directoryPath)
    {
        var result = new List<ProcessRef>();
        var root = PathGuard.Normalize(directoryPath);

        Process[] processes;
        try { processes = Process.GetProcesses(); }
        catch (Exception) { return result; }

        foreach (var p in processes)
        {
            try
            {
                string? modulePath = p.MainModule?.FileName;
                if (string.IsNullOrEmpty(modulePath)) continue;

                if (PathGuard.IsSameOrUnder(modulePath, root))
                {
                    result.Add(new ProcessRef(p.Id, p.ProcessName, modulePath));
                }
            }
            catch (Exception)
            {
                // 访问别的进程的模块信息通常会因为权限被拒，跳过即可。
            }
            finally
            {
                p.Dispose();
            }
        }

        return result;
    }

    /// <summary>
    /// 抽样探测文件占用。默认最多探测 400 个文件，避免在大目录上耗时过久。
    /// </summary>
    public static LockReport Probe(IEnumerable<string> files, int maxProbe = 400)
    {
        var locked = new List<string>();
        int probed = 0;

        foreach (var file in files)
        {
            if (probed >= maxProbe) break;
            probed++;

            try
            {
                using var fs = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 1,
                    FileOptions.None);
            }
            catch (IOException)
            {
                locked.Add(file);
            }
            catch (UnauthorizedAccessException)
            {
                // 没权限不代表被占用（可能是系统文件），不计入。
            }
            catch (Exception)
            {
                // 其他异常一律忽略。
            }
        }

        return new LockReport
        {
            ProcessesFromPath = [],
            LockedSamples = locked,
            ProbedFiles = probed,
        };
    }

    /// <summary>对某个目录做完整的占用预检。</summary>
    public static LockReport Inspect(string directoryPath, int maxProbe = 400, CancellationToken ct = default)
    {
        var processes = FindProcessesFromPath(directoryPath);

        var files = new List<string>();
        try
        {
            foreach (var f in Directory.EnumerateFiles(directoryPath, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
            }))
            {
                ct.ThrowIfCancellationRequested();
                files.Add(f);
                if (files.Count >= maxProbe) break;
            }
        }
        catch (Exception) { }

        var probe = Probe(files, maxProbe);

        return new LockReport
        {
            ProcessesFromPath = processes,
            LockedSamples = probe.LockedSamples,
            ProbedFiles = probe.ProbedFiles,
        };
    }
}
