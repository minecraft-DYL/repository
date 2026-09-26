using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace OreUI.WinUI;

/// <summary>
/// OreUI 模态弹窗。把它放进页面根部 Grid 的最后一个子元素，切换
/// <see cref="IsOpen"/> 即可显示/隐藏；标题栏、内容区、按钮区分别是
/// <c>#48494A / #313233 / #48494A</c> 三段 OreUI 配色。
/// </summary>
public partial class OreModal : ContentControl
{
    /// <summary>创建一个 OreUI 模态弹窗。</summary>
    public OreModal()
    {
        DefaultStyleKey = typeof(OreModal);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Visibility = Visibility.Collapsed;
        IsTabStop = false;
    }

    /// <summary>是否打开。</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen), typeof(bool), typeof(OreModal),
        new PropertyMetadata(false, OnIsOpenChanged));

    /// <summary>标题栏文本。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(OreModal), new PropertyMetadata(string.Empty, OnTextChanged));

    /// <summary>主按钮文本；留空则不显示。</summary>
    public static readonly DependencyProperty PrimaryButtonTextProperty = DependencyProperty.Register(
        nameof(PrimaryButtonText), typeof(string), typeof(OreModal), new PropertyMetadata("确定", OnTextChanged));

    /// <summary>关闭按钮文本；留空则不显示。</summary>
    public static readonly DependencyProperty CloseButtonTextProperty = DependencyProperty.Register(
        nameof(CloseButtonText), typeof(string), typeof(OreModal), new PropertyMetadata("取消", OnTextChanged));

    /// <summary>点击遮罩或右上角关闭按钮时是否关闭。</summary>
    public static readonly DependencyProperty IsLightDismissEnabledProperty = DependencyProperty.Register(
        nameof(IsLightDismissEnabled), typeof(bool), typeof(OreModal), new PropertyMetadata(true));

    /// <summary>主按钮被点击。若设置 <c>e.Cancel = true</c> 可阻止关闭。</summary>
    public event EventHandler<OreModalClosingEventArgs>? PrimaryButtonClick;

    /// <summary>关闭按钮 / 遮罩被点击。</summary>
    public event EventHandler? Closed;

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="PrimaryButtonTextProperty"/>
    public string PrimaryButtonText
    {
        get => (string)GetValue(PrimaryButtonTextProperty);
        set => SetValue(PrimaryButtonTextProperty, value);
    }

    /// <inheritdoc cref="CloseButtonTextProperty"/>
    public string CloseButtonText
    {
        get => (string)GetValue(CloseButtonTextProperty);
        set => SetValue(CloseButtonTextProperty, value);
    }

    /// <inheritdoc cref="IsLightDismissEnabledProperty"/>
    public bool IsLightDismissEnabled
    {
        get => (bool)GetValue(IsLightDismissEnabledProperty);
        set => SetValue(IsLightDismissEnabledProperty, value);
    }

    /// <summary>在独立的 <see cref="Popup"/> 中显示一个模态框，等待用户选择。</summary>
    /// <param name="xamlRoot">宿主页面的 XamlRoot（<c>this.XamlRoot</c>）。</param>
    /// <param name="title">标题。</param>
    /// <param name="content">内容元素或文本。</param>
    /// <param name="primaryText">主按钮文本。</param>
    /// <param name="closeText">关闭按钮文本；传 null 表示只有主按钮。</param>
    /// <returns>点击主按钮返回 true，否则 false。</returns>
    public static Task<bool> ShowAsync(
        XamlRoot xamlRoot,
        string title,
        object content,
        string primaryText = "确定",
        string? closeText = "取消")
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);

        var tcs = new TaskCompletionSource<bool>();
        var modal = new OreModal
        {
            Title = title,
            Content = content,
            PrimaryButtonText = primaryText,
            CloseButtonText = closeText ?? string.Empty,
            IsOpen = true,
        };

        var popup = new Popup
        {
            XamlRoot = xamlRoot,
            ShouldConstrainToRootBounds = false,
            Child = new Grid
            {
                Width = xamlRoot.Size.Width,
                Height = xamlRoot.Size.Height,
                Children = { modal },
            },
        };

        void Finish(bool result)
        {
            // 关键：不能在 Popup 自己子元素的 Tapped / Click 正在路由的过程中同步改
            // Popup.IsOpen —— 那等于在事件还向上冒泡时把可视树拆掉，WinUI 会直接崩
            // （点遮罩关闭静态弹窗就是这样炸的）。推迟到下一帧再关。
            if (popup.IsOpen)
            {
                popup.DispatcherQueue.TryEnqueue(() =>
                {
                    if (popup.IsOpen)
                    {
                        popup.IsOpen = false;
                    }
                });
            }

            tcs.TrySetResult(result);
        }

        modal.PrimaryButtonClick += (_, _) => Finish(true);
        modal.Closed += (_, _) => Finish(false);

        popup.IsOpen = true;
        return tcs.Task;
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (GetTemplateChild("OreModalOverlay") is Border overlay)
        {
            overlay.Background = OreUIColor.Brush(OreUIPalettes.OverlayBackground);
            overlay.Tapped += (_, _) =>
            {
                if (IsLightDismissEnabled)
                {
                    Close();
                }
            };
        }

        if (GetTemplateChild("OreModalPanel") is Border panel)
        {
            panel.BorderBrush = OreUIColor.Brush(OreUIPalettes.NeutralBorder);
            panel.Background = OreUIColor.Brush(OreUIPalettes.NeutralBackgroundPressed);
        }

        if (GetTemplateChild("OreTitleArea") is Border titleArea)
        {
            titleArea.Background = OreUIColor.Brush(OreUIPalettes.NeutralBackground);
        }

        if (GetTemplateChild("OreButtonArea") is Border buttonArea)
        {
            buttonArea.Background = OreUIColor.Brush(OreUIPalettes.NeutralBackground);
        }

        // 关闭叉必须是位图（上游 .modal_close_btn_img 就是一个 <img>）。
        // 之前用字形码 &#xE711; 会变豆腐块：WinUI 默认 Button 模板里的 ContentPresenter
        // 会把 FontFamily 固定成内容字体，覆盖掉按钮上设的 Segoe Fluent Icons ——
        // 而 OreUI 那几个字体族（Minecraft Ten/Seven/Five、Noto Sans）不含图标字形。
        if (GetTemplateChild("OreCloseGlyph") is Image closeGlyph)
        {
            closeGlyph.Source = OreAssets.CloseWhite;
        }

        if (GetTemplateChild("OreModalTitleText") is TextBlock titleText)
        {
            titleText.Text = Title;
        }

        WireButton("OreCloseButton", Close);
        WireButton("OreModalCloseIcon", Close);
        WireButton("OrePrimaryButton", () =>
        {
            var args = new OreModalClosingEventArgs();
            PrimaryButtonClick?.Invoke(this, args);
            if (!args.Cancel)
            {
                IsOpen = false;
            }
        });

        SyncButtons();
    }

    private void WireButton(string partName, Action action)
    {
        if (GetTemplateChild(partName) is Button button)
        {
            button.Click += (_, _) => action();
        }
    }

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((OreModal)d).ApplyOpenVisual();

    /// <summary>
    /// 推迟一帧再改 <see cref="UIElement.Visibility"/>。
    /// 遮罩 <c>OreModalOverlay</c> 自己就是 <c>Tapped</c> 的事件源：在它的处理函数里
    /// 同步把这个元素折叠掉，等于在路由过程中拆掉事件路径，WinUI 会抛
    /// STOWED_EXCEPTION(0xc000027b) 直接 fail-fast —— 连 Application.UnhandledException
    /// 都收不到（这正是点遮罩崩溃、且日志空白的原因）。
    /// lambda 执行时才读 <see cref="IsOpen"/>，所以连续开关不会用到过期的值。
    /// </summary>
    private void ApplyOpenVisual()
    {
        if (DispatcherQueue is null)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
            Visibility = IsOpen ? Visibility.Visible : Visibility.Collapsed);
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((OreModal)d).SyncButtons();

    private void SyncButtons()
    {
        if (GetTemplateChild("OreModalTitleText") is TextBlock titleText)
        {
            titleText.Text = Title;
        }

        if (GetTemplateChild("OrePrimaryButton") is Button primary)
        {
            primary.Content = PrimaryButtonText;
            primary.Visibility = string.IsNullOrEmpty(PrimaryButtonText)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        if (GetTemplateChild("OreCloseButton") is Button close)
        {
            close.Content = CloseButtonText;
            close.Visibility = string.IsNullOrEmpty(CloseButtonText)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }

    /// <summary>关闭弹窗并抛出 <see cref="Closed"/>。</summary>
    public void Close()
    {
        // 遮罩、右上角 X 与「取消」都会走到这里；另外 OnApplyTemplate 每次重新套用模板
        // 都会再挂一遍事件，所以同一次点击可能被派发多次。挡一道，避免重复抛 Closed。
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        Closed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary><see cref="OreModal.PrimaryButtonClick"/> 的可取消事件参数。</summary>
public sealed class OreModalClosingEventArgs : EventArgs
{
    /// <summary>设为 true 可阻止弹窗关闭（例如校验未通过）。</summary>
    public bool Cancel { get; set; }
}

/// <summary>
/// OreUI 侧边栏。窄屏时以遮罩 + 滑出面板呈现，宽度 ≥1200 时自动常驻
/// （对应 Web 端 <c>@media (min-width: 1200px)</c> 的行为）。
/// </summary>
public partial class OreSidebar : ContentControl
{
    private const double PanelWidth = 238;
    private const double WideBreakpoint = 1200;

    private TranslateTransform? _panelTransform;
    private Border? _mask;
    private Grid? _panel;
    private StackPanel? _itemHost;

    /// <summary>创建一个 OreUI 侧边栏。</summary>
    public OreSidebar()
    {
        DefaultStyleKey = typeof(OreSidebar);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        SizeChanged += (_, _) => UpdateWideMode();
    }

    /// <summary>侧边栏条目集合。可在 XAML 中用 <c>&lt;oreui:OreSidebar.Items&gt;</c> 直接添加。</summary>
    public ObservableCollection<OreSidebarItem> Items { get; } = new();

    /// <summary>是否展开（常驻模式下恒为 true）。</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen), typeof(bool), typeof(OreSidebar),
        new PropertyMetadata(false, OnIsOpenChanged));

    /// <summary>面板标题。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(OreSidebar), new PropertyMetadata(string.Empty, OnHeaderChanged));

    /// <summary>标题下方的副标题 / 版本号。</summary>
    public static readonly DependencyProperty DetailProperty = DependencyProperty.Register(
        nameof(Detail), typeof(string), typeof(OreSidebar), new PropertyMetadata(string.Empty, OnHeaderChanged));

    /// <summary>当前选中项索引；-1 表示无选中。</summary>
    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex), typeof(int), typeof(OreSidebar),
        new PropertyMetadata(-1, OnSelectedIndexChanged));

    /// <summary>是否处于常驻（宽屏）模式。</summary>
    public static readonly DependencyProperty IsWideModeProperty = DependencyProperty.Register(
        nameof(IsWideMode), typeof(bool), typeof(OreSidebar),
        new PropertyMetadata(false, OnWideModeChanged));

    /// <summary>条目被点击。</summary>
    public event EventHandler<OreSidebarItemEventArgs>? ItemClick;

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="DetailProperty"/>
    public string Detail
    {
        get => (string)GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    /// <inheritdoc cref="SelectedIndexProperty"/>
    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <inheritdoc cref="IsWideModeProperty"/>
    public bool IsWideMode
    {
        get => (bool)GetValue(IsWideModeProperty);
        private set => SetValue(IsWideModeProperty, value);
    }

    /// <summary>切换展开 / 收起。</summary>
    public void Toggle() => IsOpen = !IsOpen;

    /// <inheritdoc/>
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _panelTransform = GetTemplateChild("OrePanelTransform") as TranslateTransform;
        _mask = GetTemplateChild("OreSidebarMask") as Border;
        _panel = GetTemplateChild("OreSidebarPanel") as Grid;
        _itemHost = GetTemplateChild("OreSidebarItems") as StackPanel;

        if (_mask is not null)
        {
            _mask.Background = OreUIColor.Brush(OreUIPalettes.OverlayBackground);
            _mask.Tapped += (_, _) =>
            {
                if (IsWideMode)
                {
                    return;
                }

                // 遮罩本身就是这次 Tapped 的事件源，折叠它要避开事件路由过程（同上）
                DispatcherQueue.TryEnqueue(() => IsOpen = false);
            };
        }

        if (_panel is not null)
        {
            _panel.Width = PanelWidth;
            _panel.Background = OreUIColor.Brush(OreUIPalettes.Gray80);
            _panel.BorderBrush = OreUIColor.Brush(OreUIPalettes.Gray100);
        }

        if (GetTemplateChild("OreSidebarDivider") is Border divider)
        {
            divider.BorderBrush = OreUIColor.Brush(OreUIPalettes.NeutralBackgroundPressed);
        }

        SyncHeader();
        RebuildItems();
        UpdateWideMode();
        UpdatePanelVisual(animate: false);
    }

    /// <summary>按 <see cref="Items"/> 重建面板内容。</summary>
    public void RebuildItems()
    {
        if (_itemHost is null)
        {
            return;
        }

        _itemHost.Children.Clear();
        for (var i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            item.IsSelected = i == SelectedIndex;
            item.Tapped += OnItemTapped;
            _itemHost.Children.Add(item);
        }
    }

    private void OnItemTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not OreSidebarItem item)
        {
            return;
        }

        SelectedIndex = Items.IndexOf(item);
        ItemClick?.Invoke(this, new OreSidebarItemEventArgs(item, SelectedIndex));

        if (!IsWideMode)
        {
            // 窄屏点条目会收起侧边栏（折叠面板与遮罩），同样避开事件路由过程
            DispatcherQueue.TryEnqueue(() => IsOpen = false);
        }
    }

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((OreSidebar)d).UpdatePanelVisual(animate: true);

    private static void OnWideModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((OreSidebar)d).UpdatePanelVisual(animate: true);

    private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((OreSidebar)d).SyncHeader();

    private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var sidebar = (OreSidebar)d;
        for (var i = 0; i < sidebar.Items.Count; i++)
        {
            sidebar.Items[i].IsSelected = i == sidebar.SelectedIndex;
        }
    }

    private void SyncHeader()
    {
        if (GetTemplateChild("OreSidebarTitle") is TextBlock title)
        {
            title.Text = Title;
        }

        if (GetTemplateChild("OreSidebarDetail") is TextBlock detail)
        {
            detail.Text = Detail;
            detail.Visibility = string.IsNullOrEmpty(Detail) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private void UpdateWideMode()
    {
        var wide = ActualWidth >= WideBreakpoint && ActualWidth > 0;
        if (wide != IsWideMode)
        {
            IsWideMode = wide;
        }
    }

    private void UpdatePanelVisual(bool animate)
    {
        var open = IsOpen || IsWideMode;

        if (_mask is not null)
        {
            _mask.Visibility = open && !IsWideMode ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_panelTransform is null)
        {
            return;
        }

        var target = open ? 0 : -PanelWidth;

        if (!animate || !IsLoaded)
        {
            _panelTransform.X = target;
            return;
        }

        var animation = new DoubleAnimation
        {
            To = target,
            Duration = new Duration(TimeSpan.FromMilliseconds(600)),
            // 🔴 TranslateTransform.X 是依赖动画属性，不显式打开就会被静默丢弃，
            // 侧边栏展开/收起会变成硬切而不是滑动。
            EnableDependentAnimation = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
        };
        Storyboard.SetTarget(animation, _panelTransform);
        Storyboard.SetTargetProperty(animation, "X");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }
}

/// <summary><see cref="OreSidebar.ItemClick"/> 的事件参数。</summary>
public sealed class OreSidebarItemEventArgs : EventArgs
{
    /// <summary>创建一个事件参数。</summary>
    public OreSidebarItemEventArgs(OreSidebarItem item, int index)
    {
        Item = item;
        Index = index;
    }

    /// <summary>被点击的条目。</summary>
    public OreSidebarItem Item { get; }

    /// <summary>条目索引。</summary>
    public int Index { get; }
}
