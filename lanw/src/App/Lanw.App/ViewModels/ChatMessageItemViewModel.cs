using Lanw.Chat.Utils;
using Microsoft.UI; // Colors（WinUI 3 的调色板类在 Microsoft.UI；Color 结构仍是 Windows.UI.Color）
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Lanw.App.ViewModels;

/// <summary>聊天气泡内的一段着色文本（颜色码解析结果）。</summary>
public sealed class ChatTextPartViewModel
{
    public ChatTextPartViewModel(string text, Brush? brush)
    {
        Text = text;
        Brush = brush;
    }

    /// <summary>本段文本（已剥离 §x 颜色码）。</summary>
    public string Text { get; }

    /// <summary>本段前景色；为 null 表示不设置（继承主题默认文本色）。</summary>
    public Brush? Brush { get; }
}

/// <summary>消息类型（决定气泡对齐与配色）。</summary>
public enum ChatMessageKind
{
    /// <summary>本机发送的消息。</summary>
    Sent,

    /// <summary>系统/状态消息（连接、开关等）。</summary>
    System,

    /// <summary>错误消息（连接失败、发送失败等）。</summary>
    Error,
}

/// <summary>
/// 聊天页的一条消息：本机发送回显 或 系统状态条目。
/// 文本经 <c>Lanw.Chat.Utils.MinecraftColorCodeConverter</c> 解析 §x 颜色码后按段着色
/// （纯文本无颜色码时该转换器返回空 extra，此处整段按默认前景色显示）。
/// </summary>
public sealed class ChatMessageItemViewModel
{
    /// <summary>MC 颜色名 → 屏幕显示色（原版 16 色；黑色/深灰在深色主题下不可读，桌面端做了提亮）。</summary>
    private static readonly Dictionary<string, Color> Palette = new(StringComparer.Ordinal)
    {
        ["black"] = Color.FromArgb(255, 0x8C, 0x8C, 0x8C), // 原版 #000000 → 桌面端提亮
        ["dark_blue"] = Color.FromArgb(255, 0x2B, 0x2B, 0xC8),
        ["dark_green"] = Color.FromArgb(255, 0x00, 0xAA, 0x00),
        ["dark_aqua"] = Color.FromArgb(255, 0x00, 0xAA, 0xAA),
        ["dark_red"] = Color.FromArgb(255, 0xC8, 0x2B, 0x2B),
        ["dark_purple"] = Color.FromArgb(255, 0xAA, 0x00, 0xAA),
        ["gold"] = Color.FromArgb(255, 0xFF, 0xAA, 0x00),
        ["gray"] = Color.FromArgb(255, 0xAA, 0xAA, 0xAA),
        ["dark_gray"] = Color.FromArgb(255, 0x7A, 0x7A, 0x7A), // 原版 #555555 → 桌面端略提亮
        ["blue"] = Color.FromArgb(255, 0x55, 0x55, 0xFF),
        ["green"] = Color.FromArgb(255, 0x55, 0xFF, 0x55),
        ["aqua"] = Color.FromArgb(255, 0x55, 0xFF, 0xFF),
        ["red"] = Color.FromArgb(255, 0xFF, 0x55, 0x55),
        ["light_purple"] = Color.FromArgb(255, 0xFF, 0x55, 0xFF),
        ["yellow"] = Color.FromArgb(255, 0xFF, 0xFF, 0x55),
        ["white"] = Color.FromArgb(255, 0xFF, 0xFF, 0xFF),
    };

    public ChatMessageItemViewModel(ChatMessageKind kind, string text, string sender, DateTimeOffset time)
    {
        Kind = kind;
        Sender = sender;
        Time = time;
        RawText = text;
        Parts = BuildParts(text, DefaultTextBrush(kind));
    }

    /// <summary>消息类型。</summary>
    public ChatMessageKind Kind { get; }

    /// <summary>发送者展示名（「我」或「聊天室」）。</summary>
    public string Sender { get; }

    /// <summary>时间。</summary>
    public DateTimeOffset Time { get; }

    /// <summary>原始文本（含颜色码，供排查）。</summary>
    public string RawText { get; }

    /// <summary>着色文本段。</summary>
    public IReadOnlyList<ChatTextPartViewModel> Parts { get; }

    /// <summary>气泡头部（发送者 + 时间）。</summary>
    public string Header => $"{Sender} · {Time:HH:mm:ss}";

    /// <summary>气泡对齐：本机发送靠右，其它靠左。</summary>
    public HorizontalAlignment Alignment =>
        Kind == ChatMessageKind.Sent ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    /// <summary>气泡背景。</summary>
    public Brush BubbleBackground => Kind switch
    {
        ChatMessageKind.Sent => ThemeBrush("AccentFillColorDefaultBrush", Color.FromArgb(255, 0x00, 0x78, 0xD4)),
        ChatMessageKind.Error => ThemeBrush("SystemFillColorCriticalBackgroundBrush", Color.FromArgb(255, 0xFD, 0xE7, 0xE9)),
        _ => ThemeBrush("CardBackgroundFillColorDefaultBrush", Color.FromArgb(20, 0x80, 0x80, 0x80)),
    };

    /// <summary>气泡边框（系统/错误条目带边框，便于与发送消息区分）。</summary>
    public Brush BubbleBorder => Kind switch
    {
        ChatMessageKind.Error => ThemeBrush("SystemFillColorCriticalBrush", Color.FromArgb(255, 0xC4, 0x2B, 0x1C)),
        ChatMessageKind.Sent => ThemeBrush("AccentFillColorDefaultBrush", Color.FromArgb(255, 0x00, 0x78, 0xD4)),
        _ => ThemeBrush("CardStrokeColorDefaultBrush", Color.FromArgb(40, 0x80, 0x80, 0x80)),
    };

    /// <summary>头部文字颜色。</summary>
    public Brush HeaderBrush => Kind switch
    {
        ChatMessageKind.Sent => ThemeBrush("TextOnAccentFillColorSecondaryBrush", Color.FromArgb(255, 0xF0, 0xF0, 0xF0)),
        ChatMessageKind.Error => ThemeBrush("SystemFillColorCriticalBrush", Color.FromArgb(255, 0xC4, 0x2B, 0x1C)),
        _ => ThemeBrush("TextFillColorTertiaryBrush", Color.FromArgb(255, 0x8A, 0x8A, 0x8A)),
    };

    /// <summary>系统条目（居中提示）。</summary>
    public static ChatMessageItemViewModel System(string text) =>
        new(ChatMessageKind.System, text, "聊天室", DateTimeOffset.Now);

    /// <summary>错误条目。</summary>
    public static ChatMessageItemViewModel Error(string text) =>
        new(ChatMessageKind.Error, text, "聊天室", DateTimeOffset.Now);

    /// <summary>本机发送回显。</summary>
    public static ChatMessageItemViewModel Sent(string text, string sender = "我") =>
        new(ChatMessageKind.Sent, text, sender, DateTimeOffset.Now);

    /// <summary>整段文本的默认前景色（null = 继承主题默认文本色）。</summary>
    private static Brush? DefaultTextBrush(ChatMessageKind kind) => kind switch
    {
        ChatMessageKind.Sent => ThemeBrush("TextOnAccentFillColorPrimaryBrush", Colors.White),
        ChatMessageKind.Error => ThemeBrush("SystemFillColorCriticalBrush", Color.FromArgb(255, 0xC4, 0x2B, 0x1C)),
        _ => null,
    };

    private static IReadOnlyList<ChatTextPartViewModel> BuildParts(string text, Brush? defaultBrush)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [new ChatTextPartViewModel(string.Empty, defaultBrush)];
        }

        var parsed = MinecraftColorCodeConverter.ParseColoredString(text);
        if (parsed.Extra.Count == 0)
        {
            // 纯文本（无 §x 颜色码）时转换器返回空 extra：整段按默认前景色显示。
            return [new ChatTextPartViewModel(text, defaultBrush)];
        }

        var parts = new List<ChatTextPartViewModel>(parsed.Extra.Count);
        foreach (var part in parsed.Extra)
        {
            var brush = part.Color is not null && Palette.TryGetValue(part.Color, out var color)
                ? new SolidColorBrush(color)
                : defaultBrush;
            parts.Add(new ChatTextPartViewModel(part.Text, brush));
        }

        return parts;
    }

    /// <summary>安全解析主题资源（键缺失时用兜底色，避免页面因资源键变化崩溃）。</summary>
    private static Brush ThemeBrush(string key, Color fallback)
    {
        try
        {
            if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Brush brush)
            {
                return brush;
            }
        }
        catch (Exception)
        {
            // 忽略：回退到兜底色
        }

        return new SolidColorBrush(fallback);
    }
}
