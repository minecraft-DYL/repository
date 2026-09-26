using Microsoft.UI.Xaml.Media;

namespace Lanw.App.Services;

/// <summary>
/// 皮肤图片地址解析（与 <see cref="ServerImageLoader"/> 同口径）：
/// - 远端地址（http/https）：直接作为图片源；
/// - 站点相对路径 "/image/skin/{id}.png"：CacheManager 命中本地缓存后会把皮肤图地址改写成这种形式
///   （t15 移植中已省略该副作用，地址保持远端 URL；保留该分支以便缓存模块落地后无需改界面）；
/// - 地址为空或本地文件不存在：返回 null，由界面显示占位图标（避免破图）。
/// </summary>
public static class SkinImageLoader
{
    /// <summary>把皮肤图片地址转换为可绑定到 Image.Source 的图片源（无法解析时返回 null）。</summary>
    public static ImageSource? Create(string? url)
        => ServerImageLoader.Resolve(url) is { } uri ? new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(uri) : null;
}
