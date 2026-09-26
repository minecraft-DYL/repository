using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace OreUI.WinUI;

/// <summary>
/// OreUI 立体按钮。沿用 WinUI 3 <see cref="Button"/> 的全部用法
/// （<c>Content</c> / <c>Click</c> / <c>Command</c> / <c>IsEnabled</c>），
/// 额外提供 OreUI 的 <see cref="Status"/>、<see cref="Size"/>、
/// <see cref="Type"/>、<see cref="IconGlyph"/> 等外观属性。
/// </summary>
public sealed class OreButton : Button
{
    /// <summary>按下时按钮下沉并缩短的像素数（Web 端 <c>--oreui-btn-active-offset</c>）。</summary>
    public const double ActiveOffset = 4;

    private readonly OreBevelPresenter _bevel;
    private OreElementState _state = OreElementState.Normal;
    private Thickness _baseMargin;
    private double _baseHeight;
    private bool _pressGeometryApplied;
    private DispatcherTimer? _countdownTimer;
    private string _countdownBaseText = string.Empty;
    private OreStatus _countdownBaseStatus = OreStatus.Normal;

    /// <summary>创建一个 OreUI 按钮。</summary>
    public OreButton()
    {
        DefaultStyleKey = typeof(OreButton);
        _bevel = new OreBevelPresenter(this);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>语义配色。</summary>
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status), typeof(OreStatus), typeof(OreButton),
        new PropertyMetadata(OreStatus.Normal, OnVisualChanged));

    /// <summary>按钮宽度档位。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(OreButtonSize), typeof(OreButton),
        new PropertyMetadata(OreButtonSize.Middle, OnVisualChanged));

    /// <summary>按钮外观变体。</summary>
    public static readonly DependencyProperty TypeProperty = DependencyProperty.Register(
        nameof(Type), typeof(OreButtonType), typeof(OreButton),
        new PropertyMetadata(OreButtonType.Default, OnVisualChanged));

    /// <summary>Segoe Fluent Icons 字形（如 <c>"&#xE768;"</c>）；与 <see cref="IconSource"/> 二选一。</summary>
    public static readonly DependencyProperty IconGlyphProperty = DependencyProperty.Register(
        nameof(IconGlyph), typeof(string), typeof(OreButton),
        new PropertyMetadata(null, OnIconChanged));

    /// <summary>位图图标；与 <see cref="IconGlyph"/> 二选一。</summary>
    public static readonly DependencyProperty IconSourceProperty = DependencyProperty.Register(
        nameof(IconSource), typeof(ImageSource), typeof(OreButton),
        new PropertyMetadata(null, OnIconChanged));

    /// <summary>图标相对文字的方位。</summary>
    public static readonly DependencyProperty IconPositionProperty = DependencyProperty.Register(
        nameof(IconPosition), typeof(OreIconPosition), typeof(OreButton),
        new PropertyMetadata(OreIconPosition.Left, OnIconChanged));

    /// <summary>悬浮提示文本；设置后自动挂载 <see cref="ToolTipService"/>。</summary>
    public static readonly DependencyProperty TipProperty = DependencyProperty.Register(
        nameof(Tip), typeof(string), typeof(OreButton),
        new PropertyMetadata(null, OnTipChanged));

    /// <summary>倒计时秒数；大于 0 时按钮自动进入禁用态并逐秒倒数。</summary>
    public static readonly DependencyProperty CountdownProperty = DependencyProperty.Register(
        nameof(Countdown), typeof(int), typeof(OreButton),
        new PropertyMetadata(0, OnCountdownChanged));

    /// <summary>倒计时结束（或归零）时触发。</summary>
    public event EventHandler? CountdownFinished;

    /// <inheritdoc cref="StatusProperty"/>
    public OreStatus Status
    {
        get => (OreStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    /// <inheritdoc cref="SizeProperty"/>
    public OreButtonSize Size
    {
        get => (OreButtonSize)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <inheritdoc cref="TypeProperty"/>
    public OreButtonType Type
    {
        get => (OreButtonType)GetValue(TypeProperty);
        set => SetValue(TypeProperty, value);
    }

    /// <inheritdoc cref="IconGlyphProperty"/>
    public string? IconGlyph
    {
        get => (string?)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    /// <inheritdoc cref="IconSourceProperty"/>
    public ImageSource? IconSource
    {
        get => (ImageSource?)GetValue(IconSourceProperty);
        set => SetValue(IconSourceProperty, value);
    }

    /// <inheritdoc cref="IconPositionProperty"/>
    public OreIconPosition IconPosition
    {
        get => (OreIconPosition)GetValue(IconPositionProperty);
        set => SetValue(IconPositionProperty, value);
    }

    /// <inheritdoc cref="TipProperty"/>
    public string? Tip
    {
        get => (string?)GetValue(TipProperty);
        set => SetValue(TipProperty, value);
    }

    /// <inheritdoc cref="CountdownProperty"/>
    public int Countdown
    {
        get => (int)GetValue(CountdownProperty);
        set => SetValue(CountdownProperty, value);
    }

    /// <summary>固定宽度（px），由 <see cref="Size"/> / <see cref="Type"/> 推导。</summary>
    public double ResolvedWidth => Type == OreButtonType.Sidebar
        ? 140
        : Size switch
        {
            OreButtonSize.ExtraSmall => 100,
            OreButtonSize.Small => 130,
            OreButtonSize.Large => 272,
            _ => 200,
        };

    /// <inheritdoc/>
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _bevel.Bind(GetTemplateChild);
        SyncIcons();
        ApplyVisualState();
    }

    /// <inheritdoc/>
    protected override void OnPointerEntered(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);
        SetState(OreElementState.PointerOver);
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        base.OnPointerExited(e);
        SetState(OreElementState.Normal);
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        base.OnPointerPressed(e);
        SetState(OreElementState.Pressed);
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        base.OnPointerReleased(e);
        SetState(IsPointerOver ? OreElementState.PointerOver : OreElementState.Normal);
    }

    /// <inheritdoc/>
    protected override void OnPointerCaptureLost(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        SetState(IsPointerOver ? OreElementState.PointerOver : OreElementState.Normal);
    }

    /// <inheritdoc/>
    protected override void OnPointerCanceled(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        base.OnPointerCanceled(e);
        SetState(OreElementState.Normal);
    }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var button = (OreButton)d;
        button.Width = button.ResolvedWidth;
        button.ApplyVisualState();
    }

    private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((OreButton)d).SyncIcons();

    private static void OnTipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var button = (OreButton)d;
        var tip = e.NewValue as string;
        if (string.IsNullOrEmpty(tip))
        {
            ToolTipService.SetToolTip(button, null);
        }
        else
        {
            ToolTipService.SetToolTip(button, new ToolTip { Content = tip });
        }
    }

    private static void OnCountdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var button = (OreButton)d;
        var seconds = (int)e.NewValue;
        if (seconds > 0 && button.IsLoaded)
        {
            button.StartCountdown(seconds);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Width = ResolvedWidth;
        ApplyVisualState();
        if (Countdown > 0)
        {
            StartCountdown(Countdown);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => StopCountdown();

    private void SetState(OreElementState state)
    {
        if (_state == state)
        {
            return;
        }

        _state = state;
        ApplyPressGeometry(state == OreElementState.Pressed);
        ApplyVisualState();
    }

    private void ApplyVisualState()
    {
        var effective = !IsEnabled || Status == OreStatus.Disabled
            ? OreElementState.Disabled
            : _state;

        _bevel.Apply(OreUIPalettes.Button(Status, Type, effective));
    }

    /// <summary>
    /// 复刻 Web 端 <c>:active</c> 的几何变化：高度 -4px、上外边距 +4px，
    /// 因此底边保持不动、按钮整体「压下去」。
    /// </summary>
    private void ApplyPressGeometry(bool pressed)
    {
        if (pressed == _pressGeometryApplied)
        {
            return;
        }

        if (pressed)
        {
            _baseMargin = Margin;
            _baseHeight = double.IsNaN(Height) ? 40 : Height;
            Height = Math.Max(0, _baseHeight - ActiveOffset);
            Margin = new Thickness(_baseMargin.Left, _baseMargin.Top + ActiveOffset, _baseMargin.Right, _baseMargin.Bottom);
        }
        else
        {
            Height = _baseHeight <= 0 ? 40 : _baseHeight;
            Margin = _baseMargin;
        }

        _pressGeometryApplied = pressed;
    }

    private void SyncIcons()
    {
        var glyph = IconGlyph;
        var image = IconSource;
        var hasIcon = !string.IsNullOrEmpty(glyph) || image is not null;
        var hasText = Content is string s ? !string.IsNullOrWhiteSpace(s) : Content is not null;
        var gap = hasIcon && hasText ? 4.0 : 0.0;

        if (GetTemplateChild("OreLeftGlyph") is FontIcon leftGlyph)
        {
            leftGlyph.Glyph = IconPosition == OreIconPosition.Left ? glyph ?? string.Empty : string.Empty;
            leftGlyph.Visibility = IconPosition == OreIconPosition.Left && !string.IsNullOrEmpty(glyph)
                ? Visibility.Visible
                : Visibility.Collapsed;
            leftGlyph.Margin = new Thickness(0, 0, gap, 0);
            leftGlyph.FontSize = 14;
        }

        if (GetTemplateChild("OreLeftImage") is Image leftImage)
        {
            leftImage.Source = IconPosition == OreIconPosition.Left ? image : null;
            leftImage.Visibility = IconPosition == OreIconPosition.Left && image is not null
                ? Visibility.Visible
                : Visibility.Collapsed;
            leftImage.Margin = new Thickness(0, 0, gap, 0);
        }

        if (GetTemplateChild("OreRightGlyph") is FontIcon rightGlyph)
        {
            rightGlyph.Glyph = IconPosition == OreIconPosition.Right ? glyph ?? string.Empty : string.Empty;
            rightGlyph.Visibility = IconPosition == OreIconPosition.Right && !string.IsNullOrEmpty(glyph)
                ? Visibility.Visible
                : Visibility.Collapsed;
            rightGlyph.Margin = new Thickness(gap, 0, 0, 0);
            rightGlyph.FontSize = 14;
        }

        if (GetTemplateChild("OreRightImage") is Image rightImage)
        {
            rightImage.Source = IconPosition == OreIconPosition.Right ? image : null;
            rightImage.Visibility = IconPosition == OreIconPosition.Right && image is not null
                ? Visibility.Visible
                : Visibility.Collapsed;
            rightImage.Margin = new Thickness(gap, 0, 0, 0);
        }
    }

    /// <summary>开始倒计时：按钮进入禁用态并显示 <c>(Ns)</c>，结束后恢复原状态与文案。</summary>
    public void StartCountdown(int seconds)
    {
        StopCountdown();
        if (seconds <= 0)
        {
            return;
        }

        _countdownBaseText = Content as string ?? string.Empty;
        _countdownBaseStatus = Status;
        Status = OreStatus.Disabled;

        var remaining = seconds;
        Content = FormatCountdown(_countdownBaseText, remaining);

        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += (_, _) =>
        {
            remaining--;
            if (remaining > 0)
            {
                Content = FormatCountdown(_countdownBaseText, remaining);
                return;
            }

            StopCountdown();
            Status = _countdownBaseStatus;
            Content = _countdownBaseText;
            CountdownFinished?.Invoke(this, EventArgs.Empty);
        };
        _countdownTimer.Start();
    }

    /// <summary>停止正在进行的倒计时。</summary>
    public void StopCountdown()
    {
        _countdownTimer?.Stop();
        _countdownTimer = null;
    }

    private static string FormatCountdown(string text, int seconds) => $"{text}({seconds}s)";
}
