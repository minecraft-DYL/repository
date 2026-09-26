using Microsoft.UI.Xaml.Media;

namespace Lanw.App.ViewModels;

/// <summary>
/// 服务器详情页的小图（对应原 Vue ServerDetail.vue 的 .small-image 列表，点击切换主图）。
/// </summary>
public sealed class ServerImageItemViewModel
{
    public ServerImageItemViewModel(string url, ImageSource? image)
    {
        Url = url;
        Image = image;
    }

    /// <summary>原图地址（远端 URL 或站点相对路径）。</summary>
    public string Url { get; }

    /// <summary>可显示的图片源（解析失败时为 null，行内显示占位）。</summary>
    public ImageSource? Image { get; }
}
