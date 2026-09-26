using Lanw.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace Lanw.App.Controls;

/// <summary>
/// 把「已按 §x 颜色码解析好的文本段」写入 <see cref="TextBlock"/> 的 Inlines（附加属性）。
///
/// 背景：WinUI 3（Windows App SDK）没有 WPF 的 WrapPanel，多段着色文本若用水平面板排布会失去自动换行；
/// 这里改用 RichTextBlock/TextBlock 的行内 Run —— 文本正常折行，且每段可带自己的前景色。
/// 用法：<c>&lt;TextBlock TextWrapping="Wrap" controls:InlinePartsBinder.Parts="{x:Bind Parts}" /&gt;</c>
/// </summary>
public static class InlinePartsBinder
{
    /// <summary>文本段集合（<see cref="ChatTextPartViewModel"/>）。</summary>
    public static readonly DependencyProperty PartsProperty = DependencyProperty.RegisterAttached(
        "Parts",
        typeof(IEnumerable<ChatTextPartViewModel>),
        typeof(InlinePartsBinder),
        new PropertyMetadata(null, OnPartsChanged));

    public static void SetParts(DependencyObject element, IEnumerable<ChatTextPartViewModel>? value)
        => element.SetValue(PartsProperty, value);

    public static IEnumerable<ChatTextPartViewModel>? GetParts(DependencyObject element)
        => (IEnumerable<ChatTextPartViewModel>?)element.GetValue(PartsProperty);

    private static void OnPartsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock textBlock)
        {
            return;
        }

        textBlock.Inlines.Clear();
        if (e.NewValue is not IEnumerable<ChatTextPartViewModel> parts)
        {
            return;
        }

        foreach (var part in parts)
        {
            var run = new Run { Text = part.Text };
            if (part.Brush is not null)
            {
                run.Foreground = part.Brush;
            }

            textBlock.Inlines.Add(run);
        }
    }
}
