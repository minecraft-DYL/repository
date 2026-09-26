using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace OreUI.WinUI;

/// <summary>
/// OreUI 自绘控件的基类：统一接管指针 / 启用状态，把「交互状态 → 调色板」的映射
/// 收敛到 <see cref="GetPalette"/> 一处，派生类只负责声明属性与写模板。
/// </summary>
/// <remarks>
/// 这些控件的视觉树完全由 OreUI 模板替换，因此直接继承 <see cref="Control"/> 而不是
/// WinUI 的 <c>Slider</c>/<c>ToggleSwitch</c>（它们的模板契约会与 OreUI 立体层冲突）。
/// 公开的属性名（<c>Value</c>、<c>Minimum</c>、<c>Maximum</c>、<c>IsOn</c>、<c>IsChecked</c>…）
/// 刻意与 WinUI 保持一致，使用体验不变。
/// </remarks>
public abstract class OreControlBase : Control
{
    private OreBevelPresenter? _bevel;
    private OreElementState _state = OreElementState.Normal;
    private bool _isHovered;

    /// <summary>创建一个 OreUI 自绘控件。</summary>
    protected OreControlBase()
    {
        DefaultStyleKey = GetType();
        IsTabStop = false;
        Loaded += (_, _) => ApplyPalette();
        RegisterPropertyChangedCallback(IsEnabledProperty, (_, _) => ApplyPalette());
    }

    /// <summary>当前原始交互状态（未叠加禁用）。</summary>
    protected OreElementState InteractionState => _state;

    /// <summary>叠加了 <see cref="Control.IsEnabled"/> 之后的实际状态。</summary>
    protected OreElementState EffectiveState =>
        IsEnabled ? _state : OreElementState.Disabled;

    /// <summary>指针是否停留在控件上。</summary>
    protected bool IsHovered => _state == OreElementState.PointerOver;

    /// <summary>派生类返回当前状态对应的立体调色板。</summary>
    protected abstract OreBevelPalette GetPalette(OreElementState state);

    /// <summary>状态发生实质变化时回调（用于更新文字色等非立体部件）。</summary>
    protected virtual void OnStateVisualChanged(OreElementState state)
    {
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _bevel ??= new OreBevelPresenter(this);
        _bevel.Bind(GetTemplateChild);
        ApplyPalette();
    }

    /// <summary>重新计算并写入调色板。</summary>
    protected void ApplyPalette()
    {
        var state = EffectiveState;
        _bevel?.Apply(GetPalette(state));
        OnStateVisualChanged(state);
    }

    /// <summary>显式切换交互状态（供子控件在捕获/释放指针时调用）。</summary>
    protected void SetState(OreElementState state)
    {
        if (_state == state)
        {
            return;
        }

        _state = state;
        ApplyPalette();
    }

    /// <inheritdoc/>
    protected override void OnPointerEntered(PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);
        _isHovered = true;
        SetState(OreElementState.PointerOver);
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerRoutedEventArgs e)
    {
        base.OnPointerExited(e);
        _isHovered = false;
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
        SetState(_isHovered ? OreElementState.PointerOver : OreElementState.Normal);
    }

    /// <inheritdoc/>
    protected override void OnPointerCaptureLost(PointerRoutedEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        SetState(_isHovered ? OreElementState.PointerOver : OreElementState.Normal);
    }

    /// <inheritdoc/>
    protected override void OnPointerCanceled(PointerRoutedEventArgs e)
    {
        base.OnPointerCanceled(e);
        SetState(OreElementState.Normal);
    }

    /// <summary>模板里可选的文字元素，存在时自动套用主题字体与前景色。</summary>
    protected void SyncTextElement(string partName, double fontSize)
    {
        if (GetTemplateChild(partName) is TextBlock text)
        {
            text.FontSize = fontSize;
        }
    }
}
