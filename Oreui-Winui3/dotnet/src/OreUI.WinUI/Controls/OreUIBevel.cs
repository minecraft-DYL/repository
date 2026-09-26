using System.Collections.Generic;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace OreUI.WinUI;

/// <summary>
/// OreUI「立体层」描述。Web 端用一个 CSS <c>box-shadow</c> 的多个 <c>inset</c> 分层来
/// 表达按钮/复选框的凸起质感，桌面端没有 inset 阴影，因此拆成四个可独立着色的图层：
/// 填充 + 外描边、左上高光、右下高光、底部厚度阴影。
/// </summary>
public readonly struct OreBevelPalette
{
    /// <summary>填充层背景。</summary>
    public Brush? Fill { get; init; }

    /// <summary>填充层 2px 外描边。</summary>
    public Brush? Edge { get; init; }

    /// <summary>左上镜面高光色（对应 <c>inset 3px 3px</c>）。</summary>
    public Brush? SpecularTopLeft { get; init; }

    /// <summary>左上镜面高光厚度（左, 上, 右, 下）。</summary>
    public Thickness SpecularTopLeftThickness { get; init; }

    /// <summary>右下镜面高光色（对应 <c>inset -3px -7px</c>）。</summary>
    public Brush? SpecularBottomRight { get; init; }

    /// <summary>右下镜面高光厚度（左, 上, 右, 下）。</summary>
    public Thickness SpecularBottomRightThickness { get; init; }

    /// <summary>底部厚度阴影色（对应 <c>inset 0 -4px</c>）。</summary>
    public Brush? Shadow { get; init; }

    /// <summary>底部厚度阴影高度，0 表示不绘制（按下态即为此值）。</summary>
    public double ShadowThickness { get; init; }

    /// <summary>文字前景色。</summary>
    public Brush? Foreground { get; init; }

    /// <summary>构造一个只填色的简单调色板。</summary>
    public static OreBevelPalette Simple(Brush? fill, Brush? edge = null, Brush? foreground = null) =>
        new() { Fill = fill, Edge = edge, Foreground = foreground };
}

/// <summary>
/// 颜色解析与画刷缓存。OreUI 的所有色值都来自 <c>spec/oreui-tokens.json</c>，
/// 这里统一按 CSS 的 <c>#RRGGBB</c> / <c>#RRGGBBAA</c> 顺序解析（注意与 XAML 的
/// <c>#AARRGGBB</c> 相反）。
/// </summary>
public static class OreUIColor
{
    private static readonly Dictionary<uint, SolidColorBrush> Cache = new();
    private static readonly object Gate = new();

    /// <summary>解析 CSS 十六进制色值为 <see cref="Color"/>。</summary>
    public static Color Parse(string css)
    {
        if (string.IsNullOrWhiteSpace(css))
        {
            throw new ArgumentException("色值不能为空", nameof(css));
        }

        var hex = css.Trim().TrimStart('#');
        byte r, g, b, a = 0xFF;
        switch (hex.Length)
        {
            case 3:
                r = Dup(hex[0]);
                g = Dup(hex[1]);
                b = Dup(hex[2]);
                break;
            case 6:
                r = Pair(hex, 0);
                g = Pair(hex, 2);
                b = Pair(hex, 4);
                break;
            case 8:
                r = Pair(hex, 0);
                g = Pair(hex, 2);
                b = Pair(hex, 4);
                a = Pair(hex, 6);
                break;
            default:
                throw new ArgumentException($"无法解析色值: {css}", nameof(css));
        }

        return Color.FromArgb(a, r, g, b);
    }

    /// <summary>解析并缓存画刷。必须在 UI 线程调用。</summary>
    public static SolidColorBrush Brush(string css)
    {
        var color = Parse(css);
        var key = ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
        lock (Gate)
        {
            if (Cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var brush = new SolidColorBrush(color);
            Cache[key] = brush;
            return brush;
        }
    }

    /// <summary>带透明度倍率的画刷（用于 hover 时提高高光不透明度等场景）。</summary>
    public static SolidColorBrush WithAlpha(string css, double alpha)
    {
        var c = Parse(css);
        var a = (byte)Math.Clamp(Math.Round(alpha * 255), 0, 255);
        return new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B));
    }

    private static byte Pair(string hex, int index) =>
        byte.Parse(hex.Substring(index, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static byte Dup(char c) =>
        byte.Parse(new string(c, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}

/// <summary>
/// 把 <see cref="OreBevelPalette"/> 施加到控件模板里约定的具名元素上。
/// 模板作者只需提供名为 <c>OreFillBorder</c> / <c>OreSpecularTopLeft</c> /
/// <c>OreSpecularBottomRight</c> / <c>OreBottomShadow</c> 的元素即可（缺失的元素会被忽略）。
/// </summary>
public sealed class OreBevelPresenter
{
    /// <summary>填充 + 外描边层。</summary>
    public const string FillPartName = "OreFillBorder";

    /// <summary>左上高光层。</summary>
    public const string TopLeftPartName = "OreSpecularTopLeft";

    /// <summary>右下高光层。</summary>
    public const string BottomRightPartName = "OreSpecularBottomRight";

    /// <summary>底部厚度阴影层。</summary>
    public const string ShadowPartName = "OreBottomShadow";

    private readonly Control _host;
    private Border? _fill;
    private Border? _topLeft;
    private Border? _bottomRight;
    private Border? _shadow;

    /// <summary>创建一个绑定到指定控件模板的施加器。</summary>
    public OreBevelPresenter(Control host) => _host = host;

    /// <summary>
    /// 在 <c>OnApplyTemplate</c> 中调用，抓取模板部件。
    /// <paramref name="resolver"/> 通常是宿主控件的 <c>GetTemplateChild</c>
    /// （它是 protected，所以由宿主委托进来）。
    /// </summary>
    public void Bind(Func<string, object?> resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _fill = resolver(FillPartName) as Border;
        _topLeft = resolver(TopLeftPartName) as Border;
        _bottomRight = resolver(BottomRightPartName) as Border;
        _shadow = resolver(ShadowPartName) as Border;
    }

    /// <summary>把调色板写入模板部件。</summary>
    public void Apply(in OreBevelPalette p)
    {
        if (_fill is not null)
        {
            if (p.Fill is not null)
            {
                _fill.Background = p.Fill;
            }

            if (p.Edge is not null)
            {
                _fill.BorderBrush = p.Edge;
            }
        }

        ApplyLayer(_topLeft, p.SpecularTopLeft, p.SpecularTopLeftThickness);
        ApplyLayer(_bottomRight, p.SpecularBottomRight, p.SpecularBottomRightThickness);

        if (_shadow is not null)
        {
            _shadow.Background = p.Shadow;
            _shadow.Height = p.ShadowThickness;
            _shadow.Visibility = p.ShadowThickness > 0 && p.Shadow is not null
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        if (p.Foreground is not null)
        {
            _host.Foreground = p.Foreground;
        }
    }

    private static void ApplyLayer(Border? layer, Brush? brush, Thickness thickness)
    {
        if (layer is null)
        {
            return;
        }

        layer.BorderBrush = brush;
        layer.BorderThickness = thickness;
        layer.Visibility = brush is not null && HasAny(thickness) ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool HasAny(Thickness t) =>
        t.Left > 0 || t.Top > 0 || t.Right > 0 || t.Bottom > 0;
}
