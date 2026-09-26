using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace OreUI.WinUI;

/// <summary>
/// OreUI 复选框：20x20 立体方块 + 白勾，支持可选文字标签。
/// 继承 WinUI <see cref="CheckBox"/>，因此 <c>IsChecked</c>（可空三态）、
/// <c>Click</c>、<c>Content</c> 等用法完全一致。
/// </summary>
public sealed class OreCheckBox : CheckBox
{
    private readonly OreBevelPresenter _bevel;
    private OreElementState _state = OreElementState.Normal;

    /// <summary>创建一个 OreUI 复选框。</summary>
    public OreCheckBox()
    {
        DefaultStyleKey = typeof(OreCheckBox);
        _bevel = new OreBevelPresenter(this);
        Checked += (_, _) => ApplyVisualState();
        Unchecked += (_, _) => ApplyVisualState();
        Indeterminate += (_, _) => ApplyVisualState();
        Loaded += (_, _) => ApplyVisualState();
        RegisterPropertyChangedCallback(IsEnabledProperty, (_, _) => ApplyVisualState());
    }

    /// <summary>语义状态；设为 <see cref="OreStatus.Disabled"/> 等价于禁用。</summary>
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status), typeof(OreStatus), typeof(OreCheckBox),
        new PropertyMetadata(OreStatus.Normal, (d, _) => ((OreCheckBox)d).ApplyVisualState()));

    /// <inheritdoc cref="StatusProperty"/>
    public OreStatus Status
    {
        get => (OreStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _bevel.Bind(GetTemplateChild);
        ApplyVisualState();
    }

    /// <inheritdoc/>
    protected override void OnPointerEntered(PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);
        SetState(OreElementState.PointerOver);
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerRoutedEventArgs e)
    {
        base.OnPointerExited(e);
        SetState(OreElementState.Normal);
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        base.OnPointerPressed(e);
        SetState(OreElementState.Pressed);
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        base.OnPointerReleased(e);
        SetState(IsPointerOver ? OreElementState.PointerOver : OreElementState.Normal);
    }

    private void SetState(OreElementState state)
    {
        if (_state == state)
        {
            return;
        }

        _state = state;
        ApplyVisualState();
    }

    private void ApplyVisualState()
    {
        var enabled = IsEnabled && Status != OreStatus.Disabled;
        var effective = enabled ? _state : OreElementState.Disabled;
        _bevel.Apply(OreUIPalettes.CheckBox(IsChecked == true, enabled, effective));

        if (GetTemplateChild("OreCheckGlyph") is Image glyph)
        {
            var visible = IsChecked == true;
            glyph.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

            if (enabled)
            {
                // 上游：绿色方块上叠 <img src="check_white.png">
                glyph.Source = OreAssets.CheckWhite;
                glyph.Opacity = 1;
            }
            else
            {
                // 上游禁用态是 filter: grayscale(100%) brightness(50%)，即白勾被压成 #808080。
                // 直接用半透明白勾会糊在 #D0D1D4 底上看不见，所以改用黑色勾叠加透明度，
                // 0.375 合成结果 ≈ #828283，和上游观感一致。
                glyph.Source = OreAssets.CheckBlack;
                glyph.Opacity = 0.375;
            }
        }
    }
}

/// <summary>OreUI 标签（Tag）：12px 文字 + 纯色底，用于行内状态标注。</summary>
public sealed class OreTag : ContentControl
{
    /// <summary>创建一个 OreUI 标签。</summary>
    public OreTag()
    {
        DefaultStyleKey = typeof(OreTag);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;
    }

    /// <summary>语义配色。</summary>
    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(OreAccent), typeof(OreTag),
        new PropertyMetadata(OreAccent.Neutral, OnAccentChanged));

    /// <inheritdoc cref="AccentProperty"/>
    public OreAccent Accent
    {
        get => (OreAccent)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    internal static (string Background, string Foreground) TagColors(OreAccent accent) => accent switch
    {
        OreAccent.Green => (OreUIPalettes.Green30, OreUIPalettes.Black),
        OreAccent.Blue => (OreUIPalettes.Blue10, OreUIPalettes.Black),
        OreAccent.Yellow => (OreUIPalettes.Yellow10, OreUIPalettes.Black),
        OreAccent.Red => (OreUIPalettes.Red10, OreUIPalettes.Black),
        _ => (OreUIPalettes.Gray100, OreUIPalettes.White),
    };

    private static void OnAccentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var tag = (OreTag)d;
        if (tag.GetTemplateChild("OreTagBorder") is not Border border)
        {
            return;
        }

        var (background, foreground) = TagColors(tag.Accent);
        border.Background = OreUIColor.Brush(background);
        tag.Foreground = OreUIColor.Brush(foreground);
    }
}

/// <summary>OreUI 徽标（Badge）：6x6 的纯色方点，常用于列表项前缀。</summary>
public sealed class OreBadge : Control
{
    /// <summary>创建一个 OreUI 徽标。</summary>
    public OreBadge()
    {
        DefaultStyleKey = typeof(OreBadge);
        VerticalAlignment = VerticalAlignment.Center;
    }

    /// <summary>语义配色。</summary>
    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(OreAccent), typeof(OreBadge),
        new PropertyMetadata(OreAccent.Neutral, OnAccentChanged));

    /// <inheritdoc cref="AccentProperty"/>
    public OreAccent Accent
    {
        get => (OreAccent)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    internal static string BadgeColor(OreAccent accent) => accent switch
    {
        OreAccent.Green => OreUIPalettes.Green30,
        OreAccent.Blue => OreUIPalettes.Blue10,
        OreAccent.Yellow => OreUIPalettes.Yellow10,
        OreAccent.Red => OreUIPalettes.Red10,
        _ => OreUIPalettes.White,
    };

    private static void OnAccentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var badge = (OreBadge)d;
        if (badge.GetTemplateChild("OreBadgeBorder") is Border border)
        {
            border.Background = OreUIColor.Brush(BadgeColor(badge.Accent));
        }
    }
}

/// <summary>OreUI 水平分割线（2px 实线）。</summary>
public sealed class OreDivider : Control
{
    /// <summary>创建一个 OreUI 分割线。</summary>
    public OreDivider() => DefaultStyleKey = typeof(OreDivider);
}

/// <summary>OreUI 立体分割线（上下各一条 2px，模拟凹陷槽）。</summary>
public sealed class OreDividerBox : Control
{
    /// <summary>创建一个 OreUI 立体分割线。</summary>
    public OreDividerBox() => DefaultStyleKey = typeof(OreDividerBox);
}

/// <summary>OreUI 细分隔线（2px，颜色更接近背景，用于列表内部分隔）。</summary>
public sealed class OreLine : Control
{
    /// <summary>创建一个 OreUI 细分隔线。</summary>
    public OreLine() => DefaultStyleKey = typeof(OreLine);
}

/// <summary>OreUI 垂直分割线（2px 白色，两侧各 6px 外边距）。</summary>
public sealed class OreVerticalDivider : Control
{
    /// <summary>创建一个 OreUI 垂直分割线。</summary>
    public OreVerticalDivider() => DefaultStyleKey = typeof(OreVerticalDivider);
}
