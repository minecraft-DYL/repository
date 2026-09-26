using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;

namespace OreUI.WinUI;

/// <summary>
/// OreUI 消息条（Banner）：通栏提示，支持中性 / 信息 / 重要三种语义底色。
/// </summary>
public sealed class OreBanner : ContentControl
{
    /// <summary>创建一个 OreUI 消息条。</summary>
    public OreBanner()
    {
        DefaultStyleKey = typeof(OreBanner);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
    }

    /// <summary>语义类型。</summary>
    public static readonly DependencyProperty BannerTypeProperty = DependencyProperty.Register(
        nameof(BannerType), typeof(OreBannerType), typeof(OreBanner),
        new PropertyMetadata(OreBannerType.Neutral, OnTypeChanged));

    /// <inheritdoc cref="BannerTypeProperty"/>
    public OreBannerType BannerType
    {
        get => (OreBannerType)GetValue(BannerTypeProperty);
        set => SetValue(BannerTypeProperty, value);
    }

    internal static (string Background, string Foreground) BannerColors(OreBannerType type) => type switch
    {
        OreBannerType.Information => (OreUIPalettes.Blue20, OreUIPalettes.White),
        OreBannerType.Important => (OreUIPalettes.Yellow10, OreUIPalettes.Black),
        _ => (OreUIPalettes.Gray100, OreUIPalettes.White),
    };

    private static void OnTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((OreBanner)d).ApplyType();

    /// <inheritdoc/>
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        ApplyType();
    }

    private void ApplyType()
    {
        if (GetTemplateChild("OreBannerBorder") is not Border border)
        {
            return;
        }

        var (background, foreground) = BannerColors(BannerType);
        border.Background = OreUIColor.Brush(background);
        Foreground = OreUIColor.Brush(foreground);
        FontWeight = BannerType == OreBannerType.Important
            ? Microsoft.UI.Text.FontWeights.ExtraBold
            : Microsoft.UI.Text.FontWeights.Bold;
    }
}

/// <summary>
/// OreUI 链接块（ShowBlock）：标题 + 描述 + 图标，鼠标悬停时有一道斜向高光扫过。
/// 继承 <see cref="ButtonBase"/>，因此自带点击、空格/回车激活与
/// <see cref="ButtonBase.Click"/> 事件。
/// </summary>
public sealed class OreShowBlock : ButtonBase
{
    private Rectangle? _flashThick;
    private Rectangle? _flashThin;
    private TranslateTransform? _thickTransform;
    private TranslateTransform? _thinTransform;
    private Grid? _flashLayer;
    private SkewTransform? _thickSkew;
    private SkewTransform? _thinSkew;
    private Border? _border;
    private bool _flashRunning;

    /// <summary>创建一个 OreUI 链接块。</summary>
    public OreShowBlock()
    {
        DefaultStyleKey = typeof(OreShowBlock);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        Unloaded += (_, _) => _flashRunning = false;
        SizeChanged += (_, _) => UpdateFlashGeometry();
    }

    /// <summary>块标题。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(OreShowBlock), new PropertyMetadata(string.Empty, OnTitleChanged));

    /// <summary>块描述（次要文字）。</summary>
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(OreShowBlock), new PropertyMetadata(null, OnTitleChanged));

    /// <summary>标题左侧图标字形。</summary>
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(OreShowBlock), new PropertyMetadata(null, OnTitleChanged));

    /// <inheritdoc cref="TitleProperty"/>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description
    {
        get => (string?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <inheritdoc cref="GlyphProperty"/>
    public string? Glyph
    {
        get => (string?)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _border = GetTemplateChild("OreShowBlockBorder") as Border;
        _flashThick = GetTemplateChild("OreFlashThick") as Rectangle;
        _flashThin = GetTemplateChild("OreFlashThin") as Rectangle;
        _thickTransform = GetTemplateChild("OreThickTransform") as TranslateTransform;
        _thinTransform = GetTemplateChild("OreThinTransform") as TranslateTransform;
        _flashLayer = GetTemplateChild("OreFlashLayer") as Grid;
        _thickSkew = GetTemplateChild("OreThickSkew") as SkewTransform;
        _thinSkew = GetTemplateChild("OreThinSkew") as SkewTransform;
        UpdateFlashGeometry();

        if (GetTemplateChild("OreTitleText") is TextBlock title)
        {
            title.Text = Title;
        }

        if (GetTemplateChild("OreDescriptionText") is TextBlock description)
        {
            description.Text = Description ?? string.Empty;
            description.Visibility = string.IsNullOrEmpty(Description) ? Visibility.Collapsed : Visibility.Visible;
        }

        if (GetTemplateChild("OreTitleIcon") is FontIcon icon)
        {
            icon.Glyph = Glyph ?? string.Empty;
            icon.Visibility = string.IsNullOrEmpty(Glyph) ? Visibility.Collapsed : Visibility.Visible;
        }

        if (_border is not null)
        {
            _border.BorderBrush = OreUIColor.Brush(OreUIPalettes.Gray60);
            _border.Background = null;
        }

        Foreground = OreUIColor.Brush(OreUIPalettes.White);
    }

    /// <inheritdoc/>
    protected override void OnPointerEntered(PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);
        if (_border is not null)
        {
            _border.BorderBrush = OreUIColor.Brush("#6D6D6E");
            _border.Background = OreUIColor.Brush(OreUIPalettes.Gray60);
        }

        RunFlash();
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerRoutedEventArgs e)
    {
        base.OnPointerExited(e);
        if (_border is not null)
        {
            _border.BorderBrush = OreUIColor.Brush(OreUIPalettes.Gray60);
            _border.Background = null;
        }
    }

    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((OreShowBlock)d).SyncText();

    private void SyncText()
    {
        if (GetTemplateChild("OreTitleText") is TextBlock title)
        {
            title.Text = Title;
        }

        if (GetTemplateChild("OreDescriptionText") is TextBlock description)
        {
            description.Text = Description ?? string.Empty;
            description.Visibility = string.IsNullOrEmpty(Description) ? Visibility.Collapsed : Visibility.Visible;
        }

        if (GetTemplateChild("OreTitleIcon") is FontIcon icon)
        {
            icon.Glyph = Glyph ?? string.Empty;
            icon.Visibility = string.IsNullOrEmpty(Glyph) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    /// <summary>
    /// 把扫光层的几何对齐 Web 端。三个坑：
    /// <list type="number">
    /// <item>CSS 的 <c>skewX(-45deg)</c> 以元素中心为原点，而 XAML <see cref="SkewTransform"/>
    /// 默认绕 (0,0)：<c>skewX</c> 的横向位移是 <c>tan(a)*(y-originY)</c>，
    /// 原点差半个高度就等于整条高光偏了半个高度。</item>
    /// <item>CSS 靠 <c>overflow: hidden</c> 把扫光裁在方块内，XAML 的 <c>Border</c> 默认不裁剪子元素。</item>
    /// </list>
    /// </summary>
    private void UpdateFlashGeometry()
    {
        if (_flashLayer is null)
        {
            return;
        }

        var h = _flashLayer.ActualHeight;
        var w = _flashLayer.ActualWidth;

        if (_thickSkew is not null)
        {
            _thickSkew.CenterY = h / 2;
        }

        if (_thinSkew is not null)
        {
            _thinSkew.CenterY = h / 2;
        }

        if (w > 0 && h > 0)
        {
            _flashLayer.Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, w, h) };
        }
    }

    /// <summary>
    /// 复刻 Web 端 <c>thickFlash</c> / <c>thinFlash</c>：两道 15px / 6px 的斜白条
    /// 在 0.6s 内自左向右扫过。
    /// 位移范围对应 CSS 的 <c>left: -150% → 150%</c>，百分比相对自身宽度
    /// （之前用「宽度 + 60 像素」导致扫过距离和端点都不对）。
    /// </summary>
    private void RunFlash()
    {
        if (_flashRunning || _flashThick is null || _flashThin is null ||
            _thickTransform is null || _thinTransform is null)
        {
            return;
        }

        UpdateFlashGeometry();

        var width = Math.Max(_flashLayer?.ActualWidth ?? ActualWidth, 1);
        var from = -1.5 * width;
        var to = 1.5 * width;
        _flashRunning = true;

        var storyboard = new Storyboard();

        storyboard.Children.Add(BuildFlashAnimation(_thickTransform, from, to, 600));
        storyboard.Children.Add(BuildFlashAnimation(_thinTransform, from, to, 600, 10));

        storyboard.Completed += (_, _) => _flashRunning = false;
        storyboard.Begin();
    }

    private static DoubleAnimation BuildFlashAnimation(
        TranslateTransform target, double from, double to, int durationMs, int beginMs = 0)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
            BeginTime = TimeSpan.FromMilliseconds(beginMs),
            // 🔴 TranslateTransform.X 是依赖动画属性。少了这一句，整条扫光动画会被
            // 静默丢弃 —— 表现就是「高光压根没扫过去」，而不只是位置不对。
            EnableDependentAnimation = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
        };

        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, "X");
        return animation;
    }
}

/// <summary>OreUI 页面标题区：底部 2px 分隔线 + Minecraft Ten 大标题。</summary>
public sealed class OrePageTitle : ContentControl
{
    /// <summary>创建一个 OreUI 页面标题。</summary>
    public OrePageTitle()
    {
        DefaultStyleKey = typeof(OrePageTitle);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
    }
}

/// <summary>OreUI 主体块外框（2px 立体边框 + 20px 左右外边距）。</summary>
public sealed class OreMainBlockFrame : ContentControl
{
    /// <summary>创建一个 OreUI 主体块外框。</summary>
    public OreMainBlockFrame()
    {
        DefaultStyleKey = typeof(OreMainBlockFrame);
        HorizontalAlignment = HorizontalAlignment.Stretch;
    }
}

/// <summary>OreUI 主体块：居中内容，最小高度 66px。</summary>
public sealed class OreMainBlock : ContentControl
{
    /// <summary>创建一个 OreUI 主体块。</summary>
    public OreMainBlock()
    {
        DefaultStyleKey = typeof(OreMainBlock);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
    }
}

/// <summary>OreUI 主体展示块：左右无边框的横向通栏。</summary>
public sealed class OreMainDisplay : ContentControl
{
    /// <summary>创建一个 OreUI 主体展示块。</summary>
    public OreMainDisplay()
    {
        DefaultStyleKey = typeof(OreMainDisplay);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
    }
}

/// <summary>块间隙（20px 高的空白占位）。</summary>
public sealed class OreMainBlockSpacing : Control
{
    /// <summary>创建一个块间隙。</summary>
    public OreMainBlockSpacing() => DefaultStyleKey = typeof(OreMainBlockSpacing);
}
