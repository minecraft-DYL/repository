using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace OreUI.WinUI;

/// <summary>
/// OreUI 气泡提示宿主。放进页面根部（Grid 的最后一个子元素）后调用
/// <see cref="Show"/> 即可从下往上堆叠气泡；同屏最多保留 5 条，
/// 超出的会先隐藏、等位置空出来后自动恢复。
/// </summary>
public sealed class OrePopHost : Control
{
    private const int MaxVisible = 5;
    private const int FadeMs = 300;

    /// <summary>应用内默认宿主；在页面上第一个 <see cref="OrePopHost"/> 加载时自动接管。</summary>
    public static OrePopHost? Default { get; private set; }

    /// <summary>创建一个气泡宿主。</summary>
    public OrePopHost()
    {
        DefaultStyleKey = typeof(OrePopHost);
        IsHitTestVisible = false;
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        Default ??= this;
    }

    /// <summary>弹出一条气泡提示。</summary>
    /// <param name="message">提示文本。</param>
    /// <param name="duration">停留时长，默认 3 秒。</param>
    /// <param name="status">语义状态，决定文字颜色。</param>
    public void Show(string message, TimeSpan? duration = null, OrePopStatus status = OrePopStatus.None)
    {
        if (GetTemplateChild("OrePopStack") is not StackPanel stack)
        {
            return;
        }

        var (background, foreground) = PopColors(status);

        // 上游 oreui-pop 不设 font-family / font-size —— 直接继承页面正文字体。
        // 这里同样不写死字体：TextBlock 会从宿主继承 OreUI 的正文族。
        // （早先写死的 "Segoe UI" 就是「消息条不像 OreUI」的来源之一。）
        var text = new TextBlock
        {
            Text = message,
            Foreground = OreUIColor.Brush(foreground),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };

        // 逐条对齐上游 oreui-pop：
        //   background-color:#1F1F1F; color:#FFFFFF; padding:10px 20px; margin:6px 0;
        //   text-align:center; opacity:0 -> 1; transform:translateY(20px) -> 0;
        //   transition: opacity .3s ease, transform .3s ease
        // 上游没有 border、没有圆角、没有图标 —— 别自己加。
        var toast = new Border
        {
            Background = OreUIColor.Brush(background),
            Padding = new Thickness(20, 10, 20, 10),
            Margin = new Thickness(0, 6, 0, 6),
            Opacity = 0,
            Child = text,
            RenderTransform = new TranslateTransform { Y = 20 },
        };

        var lifetime = duration ?? TimeSpan.FromSeconds(3);
        if (lifetime <= TimeSpan.Zero)
        {
            lifetime = TimeSpan.FromSeconds(3);
        }

        var target = (TranslateTransform)toast.RenderTransform;

        // 先挂 Loaded 再入树。反过来的话，一旦 Loaded 在 Add 过程中就已经触发，
        // 这个处理器永远不会执行 —— 淡入动画不会开始，气泡就永久停在 Opacity=0，
        // 表现就是「点了弹出气泡没反应」。
        toast.Loaded += (_, _) =>
        {
            Animate(toast, target, 1, 0, FadeMs);

            var timer = new DispatcherTimer { Interval = lifetime };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Animate(toast, target, 0, 20, FadeMs, () =>
                {
                    stack.Children.Remove(toast);
                    RestoreHidden(stack);
                });
            };
            timer.Start();
        };

        // 新消息追加到末尾 = 视觉最下方（等价于 Web 端 column-reverse + prepend）
        stack.Children.Add(toast);
        TrimVisible(stack);
    }

    /// <summary>用默认宿主弹出一条气泡；宿主尚未加载时静默忽略。</summary>
    public static void ShowDefault(string message, TimeSpan? duration = null, OrePopStatus status = OrePopStatus.None) =>
        Default?.Show(message, duration, status);

    internal static (string Background, string Foreground) PopColors(OrePopStatus status) => status switch
    {
        OrePopStatus.Success => ("#1F1F1F", OreUIPalettes.Green30),
        OrePopStatus.Process => ("#1F1F1F", OreUIPalettes.Yellow10),
        OrePopStatus.Error => ("#1F1F1F", OreUIPalettes.Red10),
        OrePopStatus.Vip => ("#1F1F1F", "#FEE039"),
        OrePopStatus.DebugText => (OreUIPalettes.Yellow10, OreUIPalettes.Black),
        _ => ("#1F1F1F", OreUIPalettes.White),
    };

    private static void TrimVisible(StackPanel stack)
    {
        var visible = 0;
        foreach (var child in stack.Children)
        {
            if (child is UIElement element && element.Visibility == Visibility.Visible)
            {
                visible++;
            }
        }

        if (visible < MaxVisible)
        {
            return;
        }

        // 从最旧的一条（索引最小）开始隐藏；它自己的计时器仍在走，到点会自行移除
        for (var i = 0; i < stack.Children.Count; i++)
        {
            if (stack.Children[i] is UIElement element && element.Visibility == Visibility.Visible)
            {
                element.Visibility = Visibility.Collapsed;
                break;
            }
        }
    }

    private static void RestoreHidden(StackPanel stack)
    {
        var visible = 0;
        foreach (var child in stack.Children)
        {
            if (child is UIElement element && element.Visibility == Visibility.Visible)
            {
                visible++;
            }
        }

        if (visible >= MaxVisible)
        {
            return;
        }

        for (var i = stack.Children.Count - 1; i >= 0; i--)
        {
            if (stack.Children[i] is UIElement element && element.Visibility == Visibility.Collapsed)
            {
                element.Visibility = Visibility.Visible;
                break;
            }
        }
    }

    private static void Animate(
        UIElement element, TranslateTransform transform, double opacity, double offsetY, int durationMs,
        Action? completed = null)
    {
        var storyboard = new Storyboard();

        var fade = new DoubleAnimation
        {
            To = opacity,
            Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(fade, element);
        Storyboard.SetTargetProperty(fade, "Opacity");

        var slide = new DoubleAnimation
        {
            To = offsetY,
            Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
            // 🔴 同上：TranslateTransform.Y 是依赖动画属性，必须显式打开，
            // 否则这条滑入动画会被静默丢弃。
            EnableDependentAnimation = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(slide, transform);
        Storyboard.SetTargetProperty(slide, "Y");

        storyboard.Children.Add(fade);
        storyboard.Children.Add(slide);

        if (completed is not null)
        {
            storyboard.Completed += (_, _) => completed();
        }

        storyboard.Begin();
    }
}

/// <summary>
/// OreUI 加载遮罩。默认铺满父容器，显示旋转指示器与文案；
/// 也支持错误态（大图 + 红字描边）。
/// </summary>
public sealed class OreLoadingMask : Control
{
    /// <summary>创建一个加载遮罩。</summary>
    public OreLoadingMask()
    {
        DefaultStyleKey = typeof(OreLoadingMask);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
    }

    /// <summary>是否正在加载（为 false 时遮罩折叠）。</summary>
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(
        nameof(IsLoading), typeof(bool), typeof(OreLoadingMask),
        new PropertyMetadata(true, OnLoadingChanged));

    /// <summary>加载文案。</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(OreLoadingMask),
        new PropertyMetadata("加载中…", OnLoadingChanged));

    /// <summary>错误文案；非空时切换到错误态（隐藏转圈、显示红字）。</summary>
    public static readonly DependencyProperty ErrorMessageProperty = DependencyProperty.Register(
        nameof(ErrorMessage), typeof(string), typeof(OreLoadingMask),
        new PropertyMetadata(null, OnLoadingChanged));

    /// <summary>遮罩底色，默认中性灰 <c>#48494A</c>。</summary>
    public static readonly DependencyProperty MaskBackgroundProperty = DependencyProperty.Register(
        nameof(MaskBackground), typeof(Brush), typeof(OreLoadingMask),
        new PropertyMetadata(null, OnLoadingChanged));

    /// <inheritdoc cref="IsLoadingProperty"/>
    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    /// <inheritdoc cref="TextProperty"/>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <inheritdoc cref="ErrorMessageProperty"/>
    public string? ErrorMessage
    {
        get => (string?)GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    /// <inheritdoc cref="MaskBackgroundProperty"/>
    public Brush? MaskBackground
    {
        get => (Brush?)GetValue(MaskBackgroundProperty);
        set => SetValue(MaskBackgroundProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        Sync();
    }

    private static void OnLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((OreLoadingMask)d).Sync();

    private void Sync()
    {
        Visibility = IsLoading ? Visibility.Visible : Visibility.Collapsed;

        if (GetTemplateChild("OreMaskBorder") is Border border)
        {
            border.Background = MaskBackground ?? OreUIColor.Brush(OreUIPalettes.NeutralBackground);
        }

        var hasError = !string.IsNullOrEmpty(ErrorMessage);

        // 上游的加载指示器是一张动画 GIF（Loading_white.gif，60×60），
        // 不是 WinUI 的 ProgressRing —— 用转圈就会「一看还是 WinUI3」。
        // Image 会自动播放 GIF，不需要手搓 Storyboard。
        if (GetTemplateChild("OreSpinner") is Image spinner)
        {
            spinner.Source = OreAssets.LoadingWhite;
            spinner.Visibility = hasError ? Visibility.Collapsed : Visibility.Visible;
        }

        if (GetTemplateChild("OreErrorImage") is Image errorImage)
        {
            errorImage.Source = OreAssets.ErrorMessage;
            errorImage.Visibility = hasError ? Visibility.Visible : Visibility.Collapsed;
        }

        if (GetTemplateChild("OreErrorText") is TextBlock error)
        {
            error.Text = ErrorMessage ?? string.Empty;
            error.Visibility = hasError ? Visibility.Visible : Visibility.Collapsed;
        }

        if (GetTemplateChild("OreLoadingText") is TextBlock text)
        {
            text.Text = hasError ? string.Empty : Text;
            text.Visibility = hasError ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
