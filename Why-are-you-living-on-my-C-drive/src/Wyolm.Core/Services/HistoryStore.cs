using System.Text;
using System.Text.Json;
using Wyolm.Core.Models;

namespace Wyolm.Core.Services;

/// <summary>
/// 迁移历史 + 运行日志。
/// <para>历史用 JSONL 追加写，读的时候容错：某一行坏了就跳过，不会让整个历史文件作废。</para>
/// </summary>
public static class HistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly object WriteGate = new();

    /// <summary>追加一条历史记录。</summary>
    public static void Append(MigrationRecord record)
    {
        lock (WriteGate)
        {
            try
            {
                AppPaths.EnsureCreated();
                var line = JsonSerializer.Serialize(record, JsonOptions);
                File.AppendAllText(AppPaths.HistoryFile, line + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception) { }
        }
    }

    /// <summary>读取全部历史，最新的在前。</summary>
    public static IReadOnlyList<MigrationRecord> Load(int max = 500)
    {
        var list = new List<MigrationRecord>();
        try
        {
            if (!File.Exists(AppPaths.HistoryFile)) return list;

            foreach (var line in File.ReadLines(AppPaths.HistoryFile, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var record = JsonSerializer.Deserialize<MigrationRecord>(line, JsonOptions);
                    if (record is not null) list.Add(record);
                }
                catch (Exception) { }
            }
        }
        catch (Exception) { }

        return list.OrderByDescending(r => r.TimestampUtc).Take(max).ToList();
    }

    /// <summary>清空历史。</summary>
    public static void Clear()
    {
        lock (WriteGate)
        {
            try { if (File.Exists(AppPaths.HistoryFile)) File.Delete(AppPaths.HistoryFile); }
            catch (Exception) { }
        }
    }
}

/// <summary>一次迁移的详细运行日志，写到 %LOCALAPPDATA%\Wyolm\logs 下。</summary>
public sealed class MigrationLog : IDisposable
{
    private readonly string _path;
    private readonly object _gate = new();
    private readonly List<string> _buffer = [];
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private bool _disposed;

    public string Path => _path;

    /// <summary>本次作业从创建日志对象到现在经过的时间。</summary>
    public TimeSpan Elapsed => _clock.Elapsed;

    public MigrationLog(string jobId)
    {
        AppPaths.EnsureCreated();
        _path = System.IO.Path.Combine(AppPaths.LogDirectory, $"{jobId}.log");
    }

    public void Info(string message) => Write("INFO ", message);
    public void Warn(string message) => Write("WARN ", message);
    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {level} {message}";
        lock (_gate)
        {
            _buffer.Add(line);
            if (_disposed) return;
            try { File.AppendAllText(_path, line + Environment.NewLine, Encoding.UTF8); }
            catch (Exception) { }
        }
    }

    /// <summary>取回本次会话产生的全部日志行（界面上直接展示）。</summary>
    public IReadOnlyList<string> Snapshot()
    {
        lock (_gate) { return _buffer.ToList(); }
    }

    public void Dispose()
    {
        lock (_gate) { _disposed = true; }
    }
}
