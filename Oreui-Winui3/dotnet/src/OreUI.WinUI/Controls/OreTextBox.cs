using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace OreUI.WinUI;

/// <summary>
/// OreUI 文本框。继承 WinUI <see cref="TextBox"/>，<c>Text</c> / <c>PlaceholderText</c> /
/// <c>TextChanged</c> / <c>AcceptsReturn</c> / <c>IsReadOnly</c> 等用法完全一致，
/// 视觉上替换为 OreUI 的深色凹陷输入框（2px 黑边 + 顶部 4px 内阴影）。
/// </summary>
public sealed class OreTextBox : TextBox
{
    private readonly OreBevelPresenter _bevel;
    private bool _focused;

    /// <summary>创建一个 OreUI 文本框。</summary>
    public OreTextBox()
    {
        DefaultStyleKey = typeof(OreTextBox);
        _bevel = new OreBevelPresenter(this);
        GotFocus += (_, _) => { _focused = true; ApplyVisualState(); };
        LostFocus += (_, _) => { _focused = false; ApplyVisualState(); };
        PointerEntered += (_, _) => ApplyVisualState();
        PointerExited += (_, _) => ApplyVisualState();
        Loaded += (_, _) => ApplyVisualState();
        RegisterPropertyChangedCallback(IsEnabledProperty, (_, _) => ApplyVisualState());
        SelectionHighlightColor = OreUIColor.Brush("#6CC349");
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _bevel.Bind(GetTemplateChild);

        if (GetTemplateChild("OreInsetTopShadow") is Border inset)
        {
            inset.Height = 4;
            inset.Background = OreUIColor.Brush(OreUIPalettes.Gray90);
        }

        ApplyVisualState();
    }

    private void ApplyVisualState()
    {
        var enabled = IsEnabled;

        string fill, edge, insetColor, foreground, placeholder;

        if (!enabled)
        {
            fill = OreUIPalettes.Gray30;
            edge = "#8C8D90";
            insetColor = OreUIPalettes.Gray40;
            foreground = OreUIPalettes.Gray70;
            placeholder = OreUIPalettes.NeutralTextDisabled;
        }
        else
        {
            fill = OreUIPalettes.Gray80;
            // 依托 OreUI 主色做焦点指示（Web 端只靠绿色光标与选区，桌面端补一条边框更可达）
            edge = _focused ? OreUIPalettes.Green30 : OreUIPalettes.NeutralBorder;
            insetColor = OreUIPalettes.Gray90;
            foreground = OreUIPalettes.White;
            placeholder = OreUIPalettes.NeutralShadowDisabled;
        }

        _bevel.Apply(new OreBevelPalette
        {
            Fill = OreUIColor.Brush(fill),
            Edge = OreUIColor.Brush(edge),
            Foreground = OreUIColor.Brush(foreground),
        });

        if (GetTemplateChild("OreInsetTopShadow") is Border insetBorder)
        {
            insetBorder.Background = OreUIColor.Brush(insetColor);
        }

        if (GetTemplateChild("PlaceholderTextContentPresenter") is ContentPresenter hint)
        {
            hint.Foreground = OreUIColor.Brush(placeholder);
        }
    }
}

/// <summary>
/// OreUI 卡片按钮组容器：把若干按钮横向居中排列，并统一留出 OreUI 的 6px 外边距。
/// </summary>
public sealed class OreButtonGroup : ContentControl
{
    /// <summary>创建一个按钮组。</summary>
    public OreButtonGroup()
    {
        DefaultStyleKey = typeof(OreButtonGroup);
        HorizontalAlignment = HorizontalAlignment.Left;
        HorizontalContentAlignment = HorizontalAlignment.Center;
    }
}

/// <summary>OreUI 文本字段容器：输入框 + 可选的尾部按钮（如「提交」）。</summary>
public sealed class OreTextField : ContentControl
{
    /// <summary>创建一个文本字段容器。</summary>
    public OreTextField()
    {
        DefaultStyleKey = typeof(OreTextField);
        HorizontalAlignment = HorizontalAlignment.Stretch;
    }
}

/// <summary>OreUI 侧边栏条目。</summary>
public sealed class OreSidebarItem : Control
{
    /// <summary>创建一个侧边栏条目。</summary>
    public OreSidebarItem() => DefaultStyleKey = typeof(OreSidebarItem);

    /// <summary>条目文本。</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(OreSidebarItem), new PropertyMetadata(string.Empty));

    /// <summary>条目字形图标。</summary>
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(OreSidebarItem), new PropertyMetadata(null));

    /// <summary>是否处于选中态（由 <see cref="OreSidebar"/> 维护）。</summary>
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(OreSidebarItem),
        new PropertyMetadata(false, (d, _) => ((OreSidebarItem)d).Sync()));

    /// <inheritdoc cref="TextProperty"/>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <inheritdoc cref="GlyphProperty"/>
    public string? Glyph
    {
        get => (string?)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <inheritdoc cref="IsSelectedProperty"/>
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    // 这里**故意不声明 Tag**：Control 已继承 FrameworkElement.Tag（object、DP 支持）。
    // 原先自己写了一个 `public object? Tag { get; set; }`，编译器报 CS0108 —— 它把基类那个
    // 依赖属性遮住了，于是 ((FrameworkElement)item).Tag 与 item.Tag 变成**两块互不相通的存储**：
    //   * XAML 里写 Tag="x" 由生成的 XamlTypeInfo 落在派生属性上；
    //   * 任何按 FrameworkElement 访问的路径（Style 的 Setter、绑定机制、拖放、UI 自动化）
    //     读到的却是基类 DP。
    // 两边永远对不上，属于隐性正确性 bug，而且自写的普通属性没有 DP，绑不上、Style 也设不了。
    // 直接用继承来的 Tag 即可：语义一致，还顺带拿到 DP 的绑定/Style 能力。
    private bool _pointerOver;

    /// <inheritdoc/>
    protected override void OnPointerEntered(PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);
        _pointerOver = true;
        Sync();
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerRoutedEventArgs e)
    {
        base.OnPointerExited(e);
        _pointerOver = false;
        Sync();
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        Sync();
    }

    private void Sync()
    {
        if (GetTemplateChild("OreItemText") is TextBlock text)
        {
            text.Text = Text;
        }

        if (GetTemplateChild("OreItemIcon") is FontIcon icon)
        {
            icon.Glyph = Glyph ?? string.Empty;
            icon.Visibility = string.IsNullOrEmpty(Glyph) ? Visibility.Collapsed : Visibility.Visible;
        }

        if (GetTemplateChild("OreItemBorder") is Border border)
        {
            // 上游：
            //   oreui-sidebar-item:hover, :active { background-color: var(--neutral-background-hovered) }
            // 注意这里**不能**赋 null：Border.Background=null 会让整条不再参与命中测试，
            // 结果 hover 和 Tapped 全都收不到。正常态用全透明画刷代替。
            border.Background = OreUIColor.Brush(
                IsSelected
                    ? OreUIPalettes.NeutralBackgroundPressed
                    : _pointerOver
                        ? OreUIPalettes.NeutralBackgroundHovered
                        : "#00000000");
        }
    }
}
