using Microsoft.UI.Xaml.Media.Imaging;

namespace OreUI.WinUI;

/// <summary>
/// 随库分发的 OreUI 原版位图资源。
/// 注意：库的 <c>Content</c> 会被复制到以程序集名命名的子目录
/// （实测 <c>&lt;app&gt;\OreUI.WinUI\Assets\OreUI\Images\</c>），
/// 所以这里的 URI 必须带 <c>OreUI.WinUI/</c> 前缀，否则静默取不到图。
/// </summary>
public static class OreAssets
{
    private const string Root = "ms-appx:///OreUI.WinUI/Assets/OreUI/Images/";

    private static BitmapImage? _checkWhite;
    private static BitmapImage? _checkBlack;
    private static BitmapImage? _closeWhite;
    private static BitmapImage? _closeBlack;
    private static BitmapImage? _loadingWhite;
    private static BitmapImage? _loadingBlack;
    private static BitmapImage? _errorMessage;

    /// <summary>白色勾选图（上游 <c>check_white.png</c>），用于正常选中态。</summary>
    public static BitmapImage CheckWhite => _checkWhite ??= Load("check_white.png");

    /// <summary>黑色勾选图（上游 <c>check.png</c>），配合透明度合成禁用态的灰勾。</summary>
    public static BitmapImage CheckBlack => _checkBlack ??= Load("check.png");

    /// <summary>
    /// 白色关闭叉（上游 <c>cross_white.png</c>，原图 52×52，按 <c>.modal_close_btn_img</c>
    /// 显示为 20×20）。弹窗右上角的关闭键走图片而不是字体图标：上游 CSS 里
    /// <c>.modal_close_btn_img</c> 本来就是一个 <c>&lt;img&gt;</c>；而且 WinUI 默认 Button 模板的
    /// ContentPresenter 会把 FontFamily 固定成内容字体，直接写 Segoe Fluent Icons 的字形码
    /// 会被它覆盖 —— 这正是「X 号是豆腐块」的根因。
    /// </summary>
    public static BitmapImage CloseWhite => _closeWhite ??= Load("cross_white.png");

    /// <summary>黑色关闭叉（上游 <c>cross.png</c>），留给浅色背景使用。</summary>
    public static BitmapImage CloseBlack => _closeBlack ??= Load("cross.png");

    /// <summary>
    /// 白色加载指示器（上游 <c>Loading_white.gif</c>，原图 144×144，按 <c>.spinner_img</c>
    /// 显示为 60×60）。这是个动图，WinUI 的 <c>Image</c> 会自己播放 —— 上游压根没有
    /// 「CSS 转圈」，所以这里既不需要 ProgressRing 也不需要手搓 Storyboard。
    /// </summary>
    public static BitmapImage LoadingWhite => _loadingWhite ??= Load("Loading_white.gif");

    /// <summary>黑色加载指示器（上游 <c>Loading.gif</c>），留给浅色背景使用。</summary>
    public static BitmapImage LoadingBlack => _loadingBlack ??= Load("Loading.gif");

    /// <summary>
    /// 错误态大图（上游 <c>ErrorMessage.png</c>，原图 160×160，按 <c>.spinner_img_error</c>
    /// 显示高度 160px）。
    /// </summary>
    public static BitmapImage ErrorMessage => _errorMessage ??= Load("ErrorMessage.png");

    private static BitmapImage Load(string file) => new(new Uri(Root + file));
}
