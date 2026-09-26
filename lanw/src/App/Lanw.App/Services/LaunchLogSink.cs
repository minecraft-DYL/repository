using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace Lanw.App.Services;

/// <summary>
/// 启动日志汇聚：把 Serilog 日志（启动器模块 Lanw.Game.Launcher 的 Log.* 输出，
/// 例如下载/解压/安装/启动失败等）转发到启动管理页的日志区。
/// 说明：应用尚未配置全局 Logger（对应后端 ViewLogger 的移植由其它任务负责），
/// 这里以「在现有 Logger 之上再挂一个 Sink」的方式接入，重复调用只会挂一次。
/// </summary>
public sealed class LaunchLogSink : ILogEventSink
{
    private static readonly Lock AttachLock = new();
    private static bool _attached;

    private readonly ConcurrentQueue<string> _pending = new();

    /// <summary>单例（全局 Logger 只挂一次）。</summary>
    public static LaunchLogSink Instance { get; } = new();

    /// <summary>新日志行（线程可能来自任意后台线程，订阅方需自行切回 UI 线程）。</summary>
    public event Action<string>? LogEmitted;

    /// <summary>把本 Sink 挂到 Serilog 全局 Logger 上（幂等）。</summary>
    public static void Attach()
    {
        lock (AttachLock)
        {
            if (_attached)
            {
                return;
            }

            try
            {
                // WriteTo.Logger(inner) 保留原有输出，再叠加本 Sink，避免覆盖别人的配置。
                Serilog.Log.Logger = new Serilog.LoggerConfiguration()
                    .WriteTo.Sink(Instance)
                    .WriteTo.Logger(Serilog.Log.Logger)
                    .CreateLogger();
                _attached = true;
            }
            catch (Exception)
            {
                // 日志接入失败不能影响业务（启动流程照常进行，只是日志区为空）
            }
        }
    }

    /// <summary>订阅时补发挂载期间产生的日志（页面进入时能看到已有日志）。</summary>
    public void Replay(Action<string> handler)
    {
        foreach (var line in _pending)
        {
            handler(line);
        }
    }

    /// <summary>清空缓冲。</summary>
    public void Clear()
    {
        while (_pending.TryDequeue(out _))
        {
        }
    }

    public void Emit(LogEvent logEvent)
    {
        string line;
        try
        {
            var message = logEvent.RenderMessage();
            var exception = logEvent.Exception is null ? string.Empty : " | " + logEvent.Exception.Message;
            line = $"[{logEvent.Timestamp:HH:mm:ss} {logEvent.Level}] {message}{exception}";
        }
        catch (Exception)
        {
            return;
        }

        // 只保留最近若干条，避免长时间运行内存无限增长
        while (_pending.Count > 500)
        {
            _pending.TryDequeue(out _);
        }

        _pending.Enqueue(line);

        try
        {
            LogEmitted?.Invoke(line);
        }
        catch (Exception)
        {
            // 订阅方异常不能影响日志链路
        }
    }
}
