using Lanw.Core.Utils;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Lanw.App.Services;

/// <summary>
/// 网络服图片地址解析：
/// - 远端地址（http/https）：直接作为图片源；
/// - 站点相对路径 "/image/net/{id}.png"：CacheManager 命中本地缓存后会把图片地址改写成这种形式
///   （原参考版本由内嵌静态站点提供），桌面端映射到本地 resources/static 目录；
/// - 本地文件不存在或地址为空：返回 null，由界面显示占位图标（避免破图）。
/// </summary>
public static class ServerImageLoader
{
    /// <summary>把网络服图片地址转换为可绑定到 Image.Source 的图片源（无法解析时返回 null）。</summary>
    public static ImageSource? Create(string? url)
        => Resolve(url) is { } uri ? new BitmapImage(uri) : null;

    /// <summary>把网络服图片地址解析为绝对 URI（无法解析时返回 null）。</summary>
    public static Uri? Resolve(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var trimmed = url.Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return Uri.TryCreate(trimmed, UriKind.Absolute, out var remote) ? remote : null;
        }

        // 站点相对路径 → 本地静态资源目录（resources/static/...）
        var relative = trimmed.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var local = Path.Combine(PathUtil.WebSitePath, relative);
        return File.Exists(local) ? new Uri(local) : null;
    }
}
