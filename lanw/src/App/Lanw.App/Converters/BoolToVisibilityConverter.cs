using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Lanw.App.Converters;

/// <summary>
/// bool → Visibility 转换器（true → Visible，false/其他 → Collapsed）。
/// 供 x:Bind 绑定布尔状态到元素可见性使用。
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility.Visible;
}
