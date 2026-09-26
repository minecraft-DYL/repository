using System.Collections.Concurrent;
using Lanw.Public.Utils.ViewLogger;
using Serilog;

namespace Lanw.App.Services;

/// <summary>
/// 日志页数据源：把 t17 移植的后端内存日志（<see cref="InMemorySink"/>）接入 UI。
/// 说明：
/// 1) 参考源是控制台程序，<c>Logger.LogoInit()</c> 直接写控制台并调用 <c>Console.Clear()</c>；
///    WinUI 桌面进程没有控制台，故这里不复用 LogoInit，而是按 LaunchLogSink 的同样做法
///    把 InMemorySink 叠加到现有 Serilog Logger 上（幂等），使各后端模块的 Log.* 都进内存队列。
/// 2) 读取即 <see cref="InMemorySink.GetLogs"/>（倒序输出，与 /api/logs 一致）。
/// </summary>
public static class ViewLogService
{
    private static readonly Lock AttachLock = new();

    private static bool _attached;

    /// <summary>把 InMemorySink 挂到全局 Serilog Logger 上（幂等，重复调用只挂一次）。</summary>
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
                // WriteTo.Logger(inner) 保留原有输出，再叠加内存 Sink，避免覆盖别人的配置。
                Log.Logger = new LoggerConfiguration()
                    .WriteTo.Sink(InMemorySink.Instance)
                    .WriteTo.Logger(Log.Logger)
                    .CreateLogger();
                _attached = true;
            }
            catch (Exception)
            {
                // 日志接入失败不能影响业务（日志页显示为空即可）
            }
        }
    }

    /// <summary>读取当前内存日志（最新一条在末尾，与参考源 /api/logs 的顺序一致）。</summary>
    public static IReadOnlyList<string> GetLogs()
    {
        try
        {
            return InMemorySink.GetLogs().ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>
    /// 清空内存日志。
    /// 注意：<see cref="InMemorySink.Clear"/> 内部先清队列再调用 <c>Console.Clear()</c>，
    /// WinUI（WinExe 无控制台）下 Console.Clear() 会抛 IOException；此处吞掉该异常，
    /// 队列已被清空，语义与后端一致。
    /// </summary>
    public static void Clear()
    {
        try
        {
            InMemorySink.Clear();
        }
        catch (Exception)
        {
            // 无控制台：忽略 Console.Clear() 的 IO 异常，内存日志已在抛异常前清空。
        }
    }
}
