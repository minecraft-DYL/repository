using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace Lanw.Public.Utils.ViewLogger;

/// <summary>
/// 内存日志接收器（由 Nirvana.Public.Utils.ViewLogger.InMemorySink 移植）。
/// 供日志页实时读取：所有日志同时写入内存队列，GetLogs 按倒序输出。
/// </summary>
public class InMemorySink : ILogEventSink {
    public static readonly InMemorySink Instance = new();

    private readonly ConcurrentBag<string> _logs = [];

    public void Emit(LogEvent logEvent)
    {
        var message = logEvent.RenderMessage();
        _logs.Add($"[{logEvent.Level}] {message}");
    }

    public static IEnumerable<string> GetLogs()
    {
        return Instance._logs.Reverse(); // 倒序输出
    }

    public static void Clear()
    {
        Instance._logs.Clear();
        Console.Clear();
    }
}
