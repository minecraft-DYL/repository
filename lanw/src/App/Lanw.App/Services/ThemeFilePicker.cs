using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Lanw.App.Services;

/// <summary>主题包选择结果（异步方法不能用 out 参数，故用结果对象回报失败原因）。</summary>
/// <param name="Json">主题文件内容（失败 / 取消时为 null）。</param>
/// <param name="FileName">文件名（含扩展名）。</param>
/// <param name="Error">失败原因（成功时为空串）。</param>
public sealed record ThemePickResult(string? Json, string FileName, string Error)
{
    /// <summary>是否选中并读取成功。</summary>
    public bool Success => Json is not null;

    /// <summary>用户是否主动取消（用于区分静默取消与真实错误）。</summary>
    public bool IsCanceled => !Success && Error == ThemeFilePicker.CanceledError;
}

/// <summary>
/// 主题包（.fant.json）选择器（对应原 Vue Home.vue 的 &lt;input accept=".fant.json"&gt; 与拖拽区）。
/// 非打包（WindowsPackageType=None）桌面应用必须把选择器关联到宿主窗口 HWND 才能弹出；
/// 窗口句柄优先取 XamlRoot 的 ContentIslandEnvironment.AppWindowId，取不到时回退到前台窗口。
/// 只接受 .fant.json；读取失败（被占用 / 无权限 / 过大）返回失败结果并给出原因，不抛到 UI 线程。
/// </summary>
public static class ThemeFilePicker
{
    /// <summary>用户取消选择时的提示文本。</summary>
    public const string CanceledError = "已取消选择文件";

    /// <summary>主题包体积上限（1 MiB）：主题包只是少量配置，超限直接拒绝。</summary>
    public const long MaxFileSizeBytes = 1024 * 1024;

    /// <summary>主题包扩展名（原版校验 file.name.endsWith('.fant.json')）。</summary>
    public const string ThemeExtension = ".fant.json";

    /// <summary>弹出主题包选择器并读取文本。</summary>
    /// <param name="xamlRoot">当前页面的 XamlRoot（用于解析宿主窗口句柄）。</param>
    public static async Task<ThemePickResult> PickThemeFileAsync(XamlRoot? xamlRoot)
    {
        try
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.Downloads,
                ViewMode = PickerViewMode.List,
            };
            picker.FileTypeFilter.Add(ThemeExtension);

            InitializeWithWindow.Initialize(picker, ResolveWindowHandle(xamlRoot));

            StorageFile? file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return new ThemePickResult(null, string.Empty, CanceledError);
            }

            return await ReadThemeFileAsync(file);
        }
        catch (Exception e)
        {
            return new ThemePickResult(null, string.Empty, "选择主题文件失败：" + e.Message);
        }
    }

    /// <summary>读取拖拽进来的存储项（对应原版 handleFileDrop → handleFile）。</summary>
    public static Task<ThemePickResult> ReadThemeFileAsync(IStorageItem? item)
    {
        if (item is not StorageFile file)
        {
            return Task.FromResult(new ThemePickResult(null, string.Empty, "请拖入一个主题文件（.fant.json）。"));
        }

        return ReadThemeFileAsync(file);
    }

    /// <summary>读取主题包文本（校验扩展名与体积）。</summary>
    public static async Task<ThemePickResult> ReadThemeFileAsync(StorageFile file)
    {
        try
        {
            if (!file.Name.EndsWith(ThemeExtension, StringComparison.OrdinalIgnoreCase))
            {
                return new ThemePickResult(null, file.Name, "请选择 Fantnel 主题 格式的文件（.fant.json）。");
            }

            var properties = await file.GetBasicPropertiesAsync();
            if (properties.Size > MaxFileSizeBytes)
            {
                return new ThemePickResult(
                    null,
                    file.Name,
                    $"主题文件过大（{properties.Size / 1024 / 1024.0:F1} MiB，上限 {MaxFileSizeBytes / 1024 / 1024} MiB）。");
            }

            var json = await File.ReadAllTextAsync(file.Path);
            return new ThemePickResult(json, file.Name, string.Empty);
        }
        catch (Exception e)
        {
            return new ThemePickResult(null, file.Name, "读取主题文件失败：" + e.Message);
        }
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
            // 内容岛环境不可用（旧系统 / 组件未初始化）：走前台窗口回退。
        }

        return GetForegroundWindow();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
