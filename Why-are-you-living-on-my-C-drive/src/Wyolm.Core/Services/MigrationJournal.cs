using System.Text.Json;
using Wyolm.Core.Models;

namespace Wyolm.Core.Services;

/// <summary>应用的本地数据目录。</summary>
public static class AppPaths
{
    /// <summary>
    /// 数据根目录。默认是 <c>%LOCALAPPDATA%\Wyolm</c>；
    /// 可以被覆盖 —— 测试用它做隔离，将来要做便携模式也靠它。
    /// </summary>
    public static string DataRoot { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wyolm");

    public static string JournalDirectory => Path.Combine(DataRoot, "journal");
    public static string HistoryFile => Path.Combine(DataRoot, "history.jsonl");
    public static string LogDirectory => Path.Combine(DataRoot, "logs");
    public static string SettingsFile => Path.Combine(DataRoot, "settings.json");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(JournalDirectory);
        Directory.CreateDirectory(LogDirectory);
    }
}

/// <summary>
/// 事务日志。
/// <para>每一步动作之前先把"我打算做什么"落盘，再做。这样即使进程被强杀、蓝屏、断电，
/// 下次启动时 <see cref="RecoveryService"/> 依然能从日志里还原现场，
/// 判断是该继续收尾还是该回滚。这是"失败自动回滚"里"失败"包含了"应用自己挂了"的那一层保障。</para>
/// </summary>
public sealed class MigrationJournal : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _filePath;
    private readonly object _gate = new();
    private bool _disposed;

    public string JobId { get; }

    public MigrationJournal(string? jobId = null)
    {
        JobId = jobId ?? DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6];
        AppPaths.EnsureCreated();
        _filePath = Path.Combine(AppPaths.JournalDirectory, JobId + ".json");
    }

    /// <summary>把当前状态原子地写入磁盘。</summary>
    public void Write(JournalEntry entry)
    {
        lock (_gate)
        {
            if (_disposed) return;

            entry.JobId = JobId;

            try
            {
                var json = JsonSerializer.Serialize(entry, JsonOptions);
                var temp = _filePath + ".tmp";
                File.WriteAllText(temp, json, System.Text.Encoding.UTF8);

                if (File.Exists(_filePath))
                    File.Replace(temp, _filePath, null, ignoreMetadataErrors: true);
                else
                    File.Move(temp, _filePath);
            }
            catch (Exception)
            {
                // 日志写不进去不应该让迁移本身失败 —— 只是失去了崩溃恢复能力。
            }
        }
    }

    /// <summary>任务正常终结，删掉日志文件。</summary>
    public void Complete()
    {
        lock (_gate)
        {
            try { if (File.Exists(_filePath)) File.Delete(_filePath); }
            catch (Exception) { }
        }
    }

    /// <summary>读出磁盘上所有还没终结的任务日志。</summary>
    public static IReadOnlyList<JournalEntry> LoadUnfinished()
    {
        var result = new List<JournalEntry>();
        try
        {
            if (!Directory.Exists(AppPaths.JournalDirectory)) return result;

            foreach (var file in Directory.EnumerateFiles(AppPaths.JournalDirectory, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file, System.Text.Encoding.UTF8);
                    var entry = JsonSerializer.Deserialize<JournalEntry>(json, JsonOptions);
                    if (entry is not null && !entry.Finished)
                        result.Add(entry);
                }
                catch (Exception)
                {
                    // 坏掉的日志文件挪走，不要卡住整个恢复流程。
                    try { File.Move(file, file + ".corrupt", overwrite: true); } catch (Exception) { }
                }
            }
        }
        catch (Exception) { }

        return result;
    }

    /// <summary>删除某个任务的日志文件。</summary>
    public static void DeleteJournal(string jobId)
    {
        try
        {
            var path = Path.Combine(AppPaths.JournalDirectory, jobId + ".json");
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception) { }
    }

    public void Dispose()
    {
        lock (_gate) { _disposed = true; }
    }
}
