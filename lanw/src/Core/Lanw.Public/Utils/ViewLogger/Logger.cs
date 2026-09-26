using Serilog;
using Serilog.Events;

namespace Lanw.Public.Utils.ViewLogger;

/// <summary>
/// 日志初始化（由 Nirvana.Public.Utils.ViewLogger.Logger 移植）。
/// 控制台按等级着色输出，并同时写入 InMemorySink 供日志页读取。
/// </summary>
public class Logger : LoggerConfiguration {
    private void SetColor(LogEventLevel level, ConsoleColor color)
    {
        WriteTo.Logger(lc => lc.Filter.ByIncludingOnly(evt => evt.Level == level).WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss}] {Message:lj}{NewLine}{Exception}", theme: new CustomConsoleTheme(color)));
    }

    public static void LogoInit()
    {
        // 配置 Serilog 日志记录
        var logger = new Logger();
        logger.MinimumLevel.Information();
        logger.SetColor(LogEventLevel.Information, ConsoleColor.Yellow);
        logger.SetColor(LogEventLevel.Warning, ConsoleColor.DarkYellow);
        logger.SetColor(LogEventLevel.Error, ConsoleColor.Red);
        logger.SetColor(LogEventLevel.Fatal, ConsoleColor.DarkRed);
        logger.SetColor(LogEventLevel.Debug, ConsoleColor.Cyan);
        logger.WriteTo.Sink(InMemorySink.Instance);
        Log.Logger = logger.CreateLogger();

        // 清空框架信息
        InMemorySink.Clear();
    }
}
