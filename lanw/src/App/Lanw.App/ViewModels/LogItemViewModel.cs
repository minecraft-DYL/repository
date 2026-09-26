using Microsoft.UI.Xaml.Media;

namespace Lanw.App.ViewModels;

/// <summary>
/// 日志行（对应原 Vue Logs.vue 的 log-item）：原文 + 级别分类 + 级别配色。
/// 级别判定顺序与参考实现 getLogLevel 完全一致（Information → Warning → Error → Fatal → Debug → default）。
/// </summary>
public sealed class LogItemViewModel
{
    /// <summary>级别（information / warning / error / fatal / debug / default）。</summary>
    public string Level { get; }

    /// <summary>日志原文（InMemorySink 格式："[Level] message"）。</summary>
    public string Line { get; }

    /// <summary>级别前景色（按当前亮/暗主题取色）。</summary>
    public Brush Foreground { get; }

    public LogItemViewModel(string line, bool lightPalette)
    {
        Line = line;
        Level = ResolveLevel(line);
        Foreground = new SolidColorBrush(ResolveColor(Level, lightPalette));
    }

    /// <summary>级别判定（顺序与参考源 getLogLevel 一致）。</summary>
    public static string ResolveLevel(string line)
    {
        if (line.Contains("[Information]", StringComparison.Ordinal))
        {
            return "information";
        }

        if (line.Contains("[Warning]", StringComparison.Ordinal))
        {
            return "warning";
        }

        if (line.Contains("[Error]", StringComparison.Ordinal))
        {
            return "error";
        }

        if (line.Contains("[Fatal]", StringComparison.Ordinal))
        {
            return "fatal";
        }

        if (line.Contains("[Debug]", StringComparison.Ordinal))
        {
            return "debug";
        }

        return "default";
    }

    /// <summary>
    /// 级别配色：暗色沿用参考源 Logs.vue 的原色（#ffcc00 / #cc9900 / #ff0000 / #cc0000 / #00ffff），
    /// 亮色下改用同色系的深色，保证跟随系统主题时都清晰可读。
    /// </summary>
    private static Windows.UI.Color ResolveColor(string level, bool lightPalette)
    {
        return level switch
        {
            // information：Yellow
            "information" => lightPalette
                ? Windows.UI.Color.FromArgb(0xFF, 0x8A, 0x6D, 0x00)
                : Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xCC, 0x00),
            // warning：DarkYellow
            "warning" => lightPalette
                ? Windows.UI.Color.FromArgb(0xFF, 0x7A, 0x5C, 0x00)
                : Windows.UI.Color.FromArgb(0xFF, 0xCC, 0x99, 0x00),
            // error：Red
            "error" => lightPalette
                ? Windows.UI.Color.FromArgb(0xFF, 0xC4, 0x2B, 0x1C)
                : Windows.UI.Color.FromArgb(0xFF, 0xFF, 0x00, 0x00),
            // fatal：DarkRed
            "fatal" => lightPalette
                ? Windows.UI.Color.FromArgb(0xFF, 0x8B, 0x1A, 0x12)
                : Windows.UI.Color.FromArgb(0xFF, 0xCC, 0x00, 0x00),
            // debug：Cyan
            "debug" => lightPalette
                ? Windows.UI.Color.FromArgb(0xFF, 0x00, 0x7B, 0x8A)
                : Windows.UI.Color.FromArgb(0xFF, 0x00, 0xFF, 0xFF),
            // default：正文色（无级别前缀时的兜底）
            _ => lightPalette
                ? Windows.UI.Color.FromArgb(0xFF, 0x1A, 0x1A, 0x1A)
                : Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF),
        };
    }
}
