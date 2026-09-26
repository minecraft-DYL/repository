using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace OreUI.WinUI;

/// <summary>
/// OreUI 滑块。属性名与 WinUI <c>Slider</c> 一致
/// （<see cref="Minimum"/> / <see cref="Maximum"/> / <see cref="Value"/> /
/// <see cref="StepFrequency"/> / <see cref="ValueChanged"/>），
/// 但视觉树完全替换为 OreUI 的 8px 轨道 + 28x28 立体滑块。
/// </summary>
public sealed class OreSlider : OreControlBase
{
    // 上游 .slider { margin:6px; border:2px solid #1E1E1F; position:relative; width:100% }
    // 而 .slider_segment / .slider_slider 都是 position:absolute —— 它们的**包含块是
    // .slider 的 padding box（2px 边框之内）**，left 的百分比也按 padding box 宽度算。
    // 所以原点要再进一个边框 = 6+2 = 8，行程宽度是 width-16，而不是按 border box 的
    // width-12 起算：按 border box 算会让原点差 2px、中途再差 2px，
    // 这正是用户报的「一丢丢偏移」。
    private const double TrackMargin = 6;
    private const double TrackBorderThickness = 2;
    private const double InnerLeft = TrackMargin + TrackBorderThickness;
    private const double ThumbSize = 28;
    private const double TrackHeight = 8;

    private static double InnerWidth(double width) =>
        Math.Max(0, width - (2 * (TrackMargin + TrackBorderThickness)));

    private Border? _process;
    private Grid? _thumb;
    private TranslateTransform? _thumbTransform;
    private Canvas? _segmentLayer;
    private bool _dragging;

    /// <summary>创建一个 OreUI 滑块。</summary>
    public OreSlider()
    {
        DefaultStyleKey = typeof(OreSlider);
        Height = 30;
        IsTabStop = true;
        SizeChanged += (_, _) => LayoutParts();
    }

    /// <summary>最小值。</summary>
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(OreSlider), new PropertyMetadata(0d, OnRangeChanged));

    /// <summary>最大值。</summary>
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(OreSlider), new PropertyMetadata(100d, OnRangeChanged));

    /// <summary>当前值。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(OreSlider), new PropertyMetadata(0d, OnValueChanged));

    /// <summary>步进；0 表示连续取值。</summary>
    public static readonly DependencyProperty StepFrequencyProperty = DependencyProperty.Register(
        nameof(StepFrequency), typeof(double), typeof(OreSlider), new PropertyMetadata(0d, OnValueChanged));

    /// <summary>分段数量；大于 0 时在轨道上绘制黑色分隔刻度（对应 Web 端的 Set Slider）。</summary>
    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
        nameof(Segments), typeof(int), typeof(OreSlider), new PropertyMetadata(0, OnValueChanged));

    /// <summary>值变化后触发。</summary>
    public event EventHandler<double>? ValueChanged;

    /// <inheritdoc cref="MinimumProperty"/>
    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <inheritdoc cref="MaximumProperty"/>
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <inheritdoc cref="ValueProperty"/>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <inheritdoc cref="StepFrequencyProperty"/>
    public double StepFrequency
    {
        get => (double)GetValue(StepFrequencyProperty);
        set => SetValue(StepFrequencyProperty, value);
    }

    /// <inheritdoc cref="SegmentsProperty"/>
    public int Segments
    {
        get => (int)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    /// <summary>归一化后的 0..1 进度。</summary>
    public double NormalizedValue
    {
        get
        {
            var span = Maximum - Minimum;
            if (span <= 0)
            {
                return 0;
            }

            return Math.Clamp((Value - Minimum) / span, 0, 1);
        }
    }

    /// <inheritdoc/>
    protected override OreBevelPalette GetPalette(OreElementState state)
    {
        var enabled = IsEnabled;

        if (_process is not null)
        {
            _process.Background = OreUIColor.Brush(enabled
                ? OreUIPalettes.Green50
                : "#CFD0D4");

            if (_process.Parent is Grid host)
            {
                host.Visibility = Visibility.Visible;
            }
        }

        return new OreBevelPalette
        {
            Fill = OreUIColor.Brush(enabled ? "#8C8D90" : "#CFD0D4"),
            Edge = OreUIColor.Brush(enabled ? OreUIPalettes.NeutralBorder : "#8C8D90"),
            SpecularTopLeft = OreUIColor.Brush(OreUIPalettes.White40),
            SpecularTopLeftThickness = new(2, 2, 0, 0),
            SpecularBottomRight = OreUIColor.Brush(OreUIPalettes.White20),
            SpecularBottomRightThickness = new(0, 0, 2, 2),
        };
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _process = GetTemplateChild("OreSliderProcess") as Border;
        _thumb = GetTemplateChild("OreSliderThumb") as Grid;
        _thumbTransform = GetTemplateChild("OreThumbTransform") as TranslateTransform;
        _segmentLayer = GetTemplateChild("OreSegmentLayer") as Canvas;
        LayoutParts();
    }

    /// <inheritdoc/>
    protected override void OnStateVisualChanged(OreElementState state)
    {
        if (_thumb is null)
        {
            return;
        }

        // 滑块自身的 hover / 按下反馈（对应 .slider_slider:hover 及 .slider_content:hover .slider_slider）
        var enabled = IsEnabled;
        var hovered = state is OreElementState.PointerOver or OreElementState.Pressed;

        // 之前这里用 _thumb.Children[1]/[2] 取高光层，但 [1] 其实是包住高光的 Grid，
        // 类型判断永远为 false => 高光/禁用态是死代码。改为按名字取具名部件。
        if (GetTemplateChild("OreThumbFill") is Border fill)
        {
            fill.Background = OreUIColor.Brush(enabled
                ? hovered ? OreUIPalettes.Gray40 : OreUIPalettes.Gray30
                : "#CFD0D4");
            fill.BorderBrush = OreUIColor.Brush(enabled ? OreUIPalettes.NeutralBorder : "#8C8D90");
        }

        // 禁用滑块同样保留高光（上游 .disabled_slider .slider_slider 是带高光的），
        // 只有开关的禁用滑块才不带高光——两者别搞混。
        if (GetTemplateChild("OreThumbSpecularTopLeft") is Border topLeft)
        {
            topLeft.BorderBrush = OreUIColor.Brush(hovered ? OreUIPalettes.White80 : OreUIPalettes.White60);
        }

        if (GetTemplateChild("OreThumbSpecularBottomRight") is Border bottomRight)
        {
            bottomRight.BorderBrush = OreUIColor.Brush(hovered ? OreUIPalettes.White60 : OreUIPalettes.White40);
        }

        if (GetTemplateChild("OreThumbBottomShadow") is Border shadow)
        {
            // 正常 inset 0 -4px #58585A；禁用 inset 0 -4px #B0B1B5（上游滑块与开关这里差 1 阶，照抄）
            shadow.Background = OreUIColor.Brush(enabled ? OreUIPalettes.Gray60 : "#B0B1B5");
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEnabled)
        {
            return;
        }

        _dragging = true;
        CapturePointer(e.Pointer);
        SetValueFromPoint(e.GetCurrentPoint(this).Position.X);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerRoutedEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging && IsEnabled)
        {
            SetValueFromPoint(e.GetCurrentPoint(this).Position.X);
            e.Handled = true;
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        ReleasePointerCapture(e.Pointer);
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsEnabled)
        {
            return;
        }

        var step = StepFrequency > 0 ? StepFrequency : Math.Max((Maximum - Minimum) / 20, 0.01);
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Right:
            case Windows.System.VirtualKey.Up:
                Value = Math.Min(Maximum, Value + step);
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.Left:
            case Windows.System.VirtualKey.Down:
                Value = Math.Max(Minimum, Value - step);
                e.Handled = true;
                break;
        }
    }

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((OreSlider)d).LayoutParts();

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var slider = (OreSlider)d;
        slider.LayoutParts();
        slider.ValueChanged?.Invoke(slider, slider.Value);
    }

    private void SetValueFromPoint(double x)
    {
        var trackWidth = InnerWidth(ActualWidth);
        if (trackWidth <= 0)
        {
            return;
        }

        var ratio = Math.Clamp((x - InnerLeft) / trackWidth, 0, 1);
        var raw = Minimum + (ratio * (Maximum - Minimum));

        if (StepFrequency > 0)
        {
            raw = Minimum + (Math.Round((raw - Minimum) / StepFrequency) * StepFrequency);
        }
        else if (Segments > 1)
        {
            var step = (Maximum - Minimum) / (Segments - 1);
            raw = Minimum + (Math.Round((raw - Minimum) / step) * step);
        }

        Value = Math.Clamp(raw, Minimum, Maximum);
    }

    private void LayoutParts()
    {
        var width = ActualWidth;
        if (double.IsNaN(width) || width <= 0)
        {
            return;
        }

        // 滑块中心对齐进度末端（复刻 CSS left:<pct>% + translateX(-50%)），
        // 因此 0% / 100% 时会各自溢出轨道 14px，与 Web 端行为一致。
        var trackWidth = InnerWidth(width);
        var ratio = NormalizedValue;
        var thumbLeft = InnerLeft + (trackWidth * ratio) - (ThumbSize / 2);

        if (_thumbTransform is not null)
        {
            _thumbTransform.X = thumbLeft;
        }

        if (_process is not null)
        {
            _process.Width = trackWidth * ratio;
        }

        BuildSegments(width);
    }

    private void BuildSegments(double width)
    {
        if (_segmentLayer is null)
        {
            return;
        }

        _segmentLayer.Children.Clear();
        if (!IsEnabled)
        {
            return;
        }

        // 刻度数量必须按**真实档位数**推，不能直接拿 Segments 当档位数：
        // gallery 里 Segments=5 而 StepFrequency=25（档位落在 0/25/50/75/100%），
        // 若按 Segments 把刻度画在 20/40/60/80%，中间档 50% 就正好卡在两条刻度正中间
        // —— 这就是「滑到 0 和 5 看不出、滑到 3 偏移很大」的根因。
        var steps = Segments;
        var span = Maximum - Minimum;
        if (StepFrequency > 0 && StepFrequency < span)
        {
            steps = (int)Math.Round(span / StepFrequency) + 1;
        }

        if (steps <= 1)
        {
            return;
        }

        var trackWidth = InnerWidth(width);

        // 只画**内部**档位（k = 1 .. steps-2）；两端由轨道端点本身表示。
        // 位置分母是 steps-1（档位间隔数），不是 steps。
        for (var k = 1; k <= steps - 2; k++)
        {
            var tick = new Border
            {
                Width = 2,
                Height = TrackHeight,
                Background = OreUIColor.Brush(OreUIPalettes.NeutralBorder),
            };

            // 上游 .slider_segment **没有** translateX(-50%)：它的**左边缘**就落在 JS
            // 计算的位置上。所以这里绝不能减 1px —— 减了反而平白造出 1px 偏移。
            Canvas.SetLeft(tick, InnerLeft + (trackWidth * k / (steps - 1)));
            Canvas.SetTop(tick, 0);
            _segmentLayer.Children.Add(tick);
        }
    }
}
