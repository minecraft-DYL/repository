using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace OreUI.WinUI;

/// <summary>
/// OreUI 开关。轨道是「左绿右灰」的硬分界渐变，28x28 的滑块在轨道上左右滑动并带回弹。
/// 属性名与 WinUI <c>ToggleSwitch</c> 保持一致（<see cref="IsOn"/> / <see cref="OnContent"/> /
/// <see cref="OffContent"/> / <see cref="Toggled"/>）。
/// </summary>
public sealed class OreToggleSwitch : OreControlBase
{
    private const double TrackWidth = 58;
    private const double TrackHeight = 24;
    private const double KnobSize = 28;

    // 上游 .switch_slider{position:absolute; left:-2px} / .switch.on .switch_slider{left:28px}
    // 是按**包含块的 padding box**（2px 边框之内）算的。而 XAML 里滑块的位移是相对轨道
    // border box（= 模板根 Grid）算的，所以两个值都要补上 2px 边框：
    //   OFF: -2 + 2 = 0  -> 滑块左边缘与轨道外左边缘齐平
    //   ON : 28 + 2 = 30 -> 滑块右边缘与轨道外右边缘齐平（30 + 28 = 58 = 轨道宽）
    // 之前用 -2/28，滑块整体偏左 2px（且 ON 时右边缘差 2px 没到边）。
    private const double KnobLeftOff = 0;
    private const double KnobLeftOn = 30;

    private TranslateTransform? _knobTransform;
    private Border? _trackLeft;
    private Border? _trackRight;

    /// <summary>创建一个 OreUI 开关。</summary>
    public OreToggleSwitch()
    {
        DefaultStyleKey = typeof(OreToggleSwitch);
        Width = TrackWidth;
        // 28 = 滑块高度。上游 .switch 的布局盒是 24，靠溢出把 28 高的滑块露出来，
        // 但 WinUI 会把这个上溢裁掉（滑块顶部缺一块）。用 28 把整块包住，
        // 相对几何（滑块底边齐轨道底边、顶部高出轨道 4px）不变且不会被裁。
        Height = KnobSize;
        IsTabStop = true;

        // 禁用/启用切换后滑块要重新落地一次。
        // 上游 .disabled_switch .switch_slider 只换配色、位置仍由 left 决定，
        // 所以位移动画一旦被丢弃（见 MoveKnob 的注解），禁用开关的滑块就会残留旧位置。
        IsEnabledChanged += (_, _) => MoveKnob(animate: false);
    }

    /// <summary>开关状态。</summary>
    public static readonly DependencyProperty IsOnProperty = DependencyProperty.Register(
        nameof(IsOn), typeof(bool), typeof(OreToggleSwitch),
        new PropertyMetadata(false, OnIsOnChanged));

    /// <summary>打开时滑块左侧显示的内容（可留空）。</summary>
    public static readonly DependencyProperty OnContentProperty = DependencyProperty.Register(
        nameof(OnContent), typeof(object), typeof(OreToggleSwitch), new PropertyMetadata("开"));

    /// <summary>关闭时滑块右侧显示的内容（可留空）。</summary>
    public static readonly DependencyProperty OffContentProperty = DependencyProperty.Register(
        nameof(OffContent), typeof(object), typeof(OreToggleSwitch), new PropertyMetadata("关"));

    /// <summary>状态切换后触发。</summary>
    public event EventHandler? Toggled;

    /// <inheritdoc cref="IsOnProperty"/>
    public bool IsOn
    {
        get => (bool)GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    /// <inheritdoc cref="OnContentProperty"/>
    public object? OnContent
    {
        get => GetValue(OnContentProperty);
        set => SetValue(OnContentProperty, value);
    }

    /// <inheritdoc cref="OffContentProperty"/>
    public object? OffContent
    {
        get => GetValue(OffContentProperty);
        set => SetValue(OffContentProperty, value);
    }

    /// <inheritdoc/>
    protected override OreBevelPalette GetPalette(OreElementState state)
    {
        var (trackOn, trackOff, _, _) = OreUIPalettes.Switch(IsOn, IsEnabled);
        _trackLeft?.SetValue(Border.BackgroundProperty, OreUIColor.Brush(trackOn));
        _trackRight?.SetValue(Border.BackgroundProperty, OreUIColor.Brush(trackOff));

        // 滑块配色（对应 .normal_switch .switch_slider / .disabled_switch .switch_slider）
        var hovered = state is OreElementState.PointerOver or OreElementState.Pressed;

        // 注意：.disabled_switch .switch_slider 并不覆盖 background-color，
        // 所以禁用态滑块底色仍是 .switch_slider 的 #D0D1D4（不是滑块轨道的 #CFD0D4）。
        var knobFill = !IsEnabled
            ? "#D0D1D4"
            : hovered ? OreUIPalettes.Gray40 : OreUIPalettes.Gray30;

        if (GetTemplateChild("OreKnobFill") is Border knobFillBorder)
        {
            knobFillBorder.Background = OreUIColor.Brush(knobFill);
            knobFillBorder.BorderBrush = OreUIColor.Brush(IsEnabled ? OreUIPalettes.NeutralBorder : "#8C8D90");
        }

        if (GetTemplateChild("OreKnobShadow") is Border knobShadow)
        {
            // 正常 inset 0 -4px #58585A；禁用 inset 0 -4px #B1B2B5
            knobShadow.Background = OreUIColor.Brush(IsEnabled ? OreUIPalettes.Gray60 : "#B1B2B5");
        }

        // 上游 .disabled_switch .switch_slider 只有 border + 底部厚度，**没有任何 inset 高光**。
        // 之前无论是否禁用都画高光，禁用开关的滑块上就会多出一道白色亮边。
        if (GetTemplateChild("OreKnobSpecularTopLeft") is Border knobTopLeft)
        {
            knobTopLeft.Visibility = IsEnabled ? Visibility.Visible : Visibility.Collapsed;
            knobTopLeft.BorderBrush = OreUIColor.Brush(hovered ? OreUIPalettes.White80 : OreUIPalettes.White60);
        }

        if (GetTemplateChild("OreKnobSpecularBottomRight") is Border knobBottomRight)
        {
            knobBottomRight.Visibility = IsEnabled ? Visibility.Visible : Visibility.Collapsed;
            knobBottomRight.BorderBrush = OreUIColor.Brush(hovered ? OreUIPalettes.White60 : OreUIPalettes.White40);
        }

        return new OreBevelPalette
        {
            Fill = OreUIColor.Brush(OreUIPalettes.NeutralBorderDisabled),
            Edge = OreUIColor.Brush(IsEnabled ? OreUIPalettes.NeutralBorder : OreUIPalettes.NeutralBorderDisabled),
            SpecularTopLeft = IsEnabled ? OreUIColor.Brush(OreUIPalettes.White20) : null,
            SpecularTopLeftThickness = new(2, 2, 0, 0),
            SpecularBottomRight = IsEnabled ? OreUIColor.Brush(OreUIPalettes.White10) : null,
            SpecularBottomRightThickness = new(0, 0, 2, 2),
        };
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _knobTransform = GetTemplateChild("OreKnobTransform") as TranslateTransform;
        _trackLeft = GetTemplateChild("OreSwitchTrackLeft") as Border;
        _trackRight = GetTemplateChild("OreSwitchTrackRight") as Border;

        if (GetTemplateChild("OreSwitchKnob") is Grid knob)
        {
            knob.Width = KnobSize;
            knob.Height = KnobSize;
        }

        // 首次布局直接跳到目标位置，避免启动时看到滑块滑入动画
        MoveKnob(animate: false);
        ApplyPalette();
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        var wasPressed = InteractionState == OreElementState.Pressed;
        base.OnPointerReleased(e);
        if (wasPressed && IsEnabled)
        {
            Toggle();
        }
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsEnabled)
        {
            return;
        }

        if (e.Key is Windows.System.VirtualKey.Space or Windows.System.VirtualKey.Enter)
        {
            Toggle();
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Right)
        {
            SetOn(true);
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Left)
        {
            SetOn(false);
            e.Handled = true;
        }
    }

    /// <summary>切换开关状态。</summary>
    public void Toggle() => SetOn(!IsOn);

    /// <summary>设置为指定状态并在变化时抛出 <see cref="Toggled"/>。</summary>
    public void SetOn(bool value)
    {
        if (IsOn == value)
        {
            return;
        }

        IsOn = value;
    }

    private static void OnIsOnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var toggle = (OreToggleSwitch)d;
        toggle.MoveKnob(animate: toggle.IsLoaded);
        toggle.ApplyPalette();
        if (toggle.IsLoaded)
        {
            toggle.Toggled?.Invoke(toggle, EventArgs.Empty);
        }
    }

    /// <summary>滑块位移；<paramref name="animate"/> 复刻 Web 端 125ms 的回弹曲线。</summary>
    private void MoveKnob(bool animate)
    {
        if (_knobTransform is null)
        {
            return;
        }

        var target = IsOn ? KnobLeftOn : KnobLeftOff;

        if (!animate)
        {
            _knobTransform.X = target;
            return;
        }

        var animation = new DoubleAnimation
        {
            To = target,
            Duration = new Duration(TimeSpan.FromMilliseconds(125)),
            // 🔴 TranslateTransform.X 是「依赖动画」属性。不显式打开这个开关，WinUI 会
            // **静默丢弃**整条动画（不抛异常、不打日志、不生效），于是滑块停在上一次
            // 的位置 —— 这正是「禁用开关的滑块和启用开关对不上」的根因。
            EnableDependentAnimation = true,
            EasingFunction = new BackEase
            {
                EasingMode = EasingMode.EaseOut,
                Amplitude = 0.55,
            },
        };

        Storyboard.SetTarget(animation, _knobTransform);
        Storyboard.SetTargetProperty(animation, "X");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }
}
