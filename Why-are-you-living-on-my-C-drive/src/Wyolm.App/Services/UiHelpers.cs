using Windows.Storage.Pickers;
using Wyolm.Core.Models;

namespace Wyolm.App.Services;

/// <summary>界面层的小工具。</summary>
public static class UiHelpers
{
    /// <summary>弹出文件夹选择器。未打包的 WinUI3 应用必须先给选择器一个 HWND。</summary>
    public static async Task<string?> PickFolderAsync(string? startPath = null)
    {
        try
        {
            var picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                CommitButtonText = "选择此文件夹",
            };
            picker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
        }
        catch (Exception)
        {
            // 未打包应用在部分系统上选择器会失败；界面同时提供手输路径，所以这里返回 null 即可。
            return null;
        }
    }

    /// <summary>把某个盘下的路径补齐成"盘根 + 子目录 + 文件夹名"。</summary>
    public static string BuildDestination(string driveRoot, string subFolder, string sourceFolderName)
    {
        var root = driveRoot.TrimEnd('\\');
        if (string.IsNullOrWhiteSpace(subFolder))
            return Path.Combine(root, sourceFolderName);

        // 允许用户填 "WyolmMoved" 或 "A\B" 这样的相对子目录
        var sub = subFolder.Trim().Trim('\\', '/');
        return Path.Combine(root, sub, sourceFolderName);
    }

    public static string FormatBytes(long bytes) => DriveInfoModel.FormatBytes(bytes);

    /// <summary>把秒数格式化成好读的样子。</summary>
    public static string FormatDuration(TimeSpan span) =>
        span.TotalMinutes >= 1
            ? $"{(int)span.TotalMinutes} 分 {span.Seconds} 秒"
            : $"{span.TotalSeconds:0.#} 秒";
}
