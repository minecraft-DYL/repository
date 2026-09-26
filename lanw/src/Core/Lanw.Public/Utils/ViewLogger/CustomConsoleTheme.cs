using System.IO;
using Serilog.Sinks.SystemConsole.Themes;

namespace Lanw.Public.Utils.ViewLogger;

/// <summary>
/// 自定义单色控制台主题（由 Nirvana.Public.Utils.ViewLogger.CustomConsoleTheme 移植）。
/// </summary>
public class CustomConsoleTheme(ConsoleColor color) : ConsoleTheme {
    protected override int ResetCharCount => 0;

    public override bool CanBuffer => false;

    public override int Set(TextWriter output, ConsoleThemeStyle style)
    {
        switch (style) {
            case ConsoleThemeStyle.TertiaryText or ConsoleThemeStyle.SecondaryText:
                Console.ForegroundColor = color;
                break;
            default:
                Console.ResetColor();
                break;
        }

        return 0;
    }

    public override void Reset(TextWriter output)
    {
        Console.ResetColor();
    }
}
