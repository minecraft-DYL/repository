using Microsoft.UI.Xaml;

namespace Lanw.App.Services;

/// <summary>
/// 主题服务：跟随系统（MachineTheme）亮/暗自适应。
/// WinUI 3 中 FrameworkElement.RequestedTheme 取 ElementTheme.Default 即跟随系统与应用亮暗设置。
/// 应用级 Application.RequestedTheme 仅支持显式 Light/Dark（无 Default），故不设置即跟随系统。
/// </summary>
public static class ThemeService
{
    /// <summary>
    /// 将指定根元素设置为主题跟随系统（ElementTheme.Default）。
    /// </summary>
    public static void FollowSystemTheme(FrameworkElement root)
        => root.RequestedTheme = ElementTheme.Default;
}