using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Lanw.App.Services;

/// <summary>
/// 本地皮肤选择结果（异步方法不能使用 out 参数，故用结果对象回传失败原因）。
/// </summary>
/// <param name="Bytes">PNG 字节（失败/取消时为 null）</param>
/// <param name="FileName">文件名（含扩展名）</param>
/// <param name="Error">失败原因（成功时为空串）</param>
public sealed record SkinPickResult(byte[]? Bytes, string FileName, string Error)
{
    /// <summary>是否选中并校验通过。</summary>
    public bool Success => Bytes is not null;

    /// <summary>用户是否收到过「已取消」结果（用于区分静默取消与真实错误）。</summary>
    public bool IsCanceled => !Success && Error == SkinFilePicker.CanceledError;
}

/// <summary>
/// 本地皮肤 PNG 选择器（对应皮肤页「上传本地皮肤」的文件选择，WinUI 3 无组件化实现，自研）。
/// 非打包（WindowsPackageType=None）桌面应用必须把选择器关联到宿主窗口 HWND 才能弹出，
/// 窗口句柄优先取自 <see cref="XamlRoot.ContentIslandEnvironment"/> 的 AppWindowId，
/// 取不到时回退到前台窗口（用户刚点击按钮，前台窗口即应用主窗口）。
/// 只接受 .png；读取失败（文件被占用/无权限）返回失败结果并给出原因，不抛出到 UI 线程。
/// </summary>
public static class SkinFilePicker
{
    /// <summary>用户取消选择时的提示文本。</summary>
    public const string CanceledError = "已取消选择文件";

    /// <summary>皮肤 PNG 体积上限（4 MiB）：网易皮肤图实际为 64x64 级别，超限直接拒绝，避免把大文件塞进 JSON。</summary>
    public const long MaxFileSizeBytes = 4 * 1024 * 1024;

    /// <summary>PNG 文件头签名（\x89PNG\r\n\x1a\n）。</summary>
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// 弹出 PNG 文件选择器并读取字节。
    /// </summary>
    /// <param name="xamlRoot">当前页面的 XamlRoot（用于解析宿主窗口句柄）</param>
    /// <returns>选中并校验通过的 PNG（字节 + 文件名）；用户取消或校验失败时为失败结果，原因在 Error 中。</returns>
    public static async Task<SkinPickResult> PickPngAsync(XamlRoot? xamlRoot)
    {
        try
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                ViewMode = PickerViewMode.Thumbnail,
            };
            picker.FileTypeFilter.Add(".png");

            InitializeWithWindow.Initialize(picker, ResolveWindowHandle(xamlRoot));

            StorageFile? file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return new SkinPickResult(null, string.Empty, CanceledError);
            }

            var properties = await file.GetBasicPropertiesAsync();
            if (properties.Size > MaxFileSizeBytes)
            {
                return new SkinPickResult(
                    null,
                    file.Name,
                    $"皮肤文件过大（{properties.Size / 1024 / 1024.0:F1} MiB，上限 {MaxFileSizeBytes / 1024 / 1024} MiB）");
            }

            // 用 System.IO 按路径直接读取：.NET 10 + WinUI 3 下 IBuffer → byte[] 的 WinRT 互操作扩展
            // （WindowsRuntimeBufferExtensions.ToArray）不可用，故不经过 IBuffer 转换。
            var bytes = await File.ReadAllBytesAsync(file.Path);
            if (!IsPng(bytes))
            {
                return new SkinPickResult(null, file.Name, "所选文件不是有效的 PNG 图片（皮肤必须是 .png）");
            }

            return new SkinPickResult(bytes, file.Name, string.Empty);
        }
        catch (Exception e)
        {
            return new SkinPickResult(null, string.Empty, "读取本地皮肤文件失败：" + e.Message);
        }
    }

    /// <summary>校验 PNG 文件头。</summary>
    public static bool IsPng(byte[]? bytes)
    {
        if (bytes is null || bytes.Length < PngSignature.Length)
        {
            return false;
        }

        for (var i = 0; i < PngSignature.Length; i++)
        {
            if (bytes[i] != PngSignature[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>解析宿主窗口句柄：优先 XamlRoot 的内容岛环境，回退到前台窗口。</summary>
    private static IntPtr ResolveWindowHandle(XamlRoot? xamlRoot)
    {
        try
        {
            var windowId = xamlRoot?.ContentIslandEnvironment?.AppWindowId;
            if (windowId is { } id && id.Value != 0)
            {
                return Win32Interop.GetWindowFromWindowId(id);
            }
        }
        catch (Exception)
        {
            // 内容岛环境不可用（旧系统/组件未初始化）：走前台窗口回退。
        }

        return GetForegroundWindow();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
