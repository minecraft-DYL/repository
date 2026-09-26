using System.IO.Compression;
using System.Runtime.InteropServices;
using Lanw.Game.Launcher.Utils;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Lanw.App.Services;

/// <summary>
/// 服务器 MOD 导出为 ZIP：逐个下载资源包(.7z) → 解压 → 收集其中全部 .jar → 打包成 ZIP，由用户选择保存位置。
/// 说明：源启动器只做「下载并安装到本地」；ZIP 导出是 lanw 按需求新增的能力，下载/解压/枚举 jar 的取法与
/// InstallerService（Java 安装流程）保持一致。
/// </summary>
public static class ModsZipExporter
{
    /// <summary>下载解压打包的临时目录前缀。</summary>
    private const string StagingPrefix = "lanw-mods-";

    /// <summary>弹出「保存 ZIP 位置」选择器，返回完整路径（用户取消返回 null）。</summary>
    public static async Task<string?> PickZipPathAsync(XamlRoot? xamlRoot, string suggestedName)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.Downloads,
            SuggestedFileName = string.IsNullOrWhiteSpace(suggestedName) ? "server-mods" : suggestedName,
        };
        picker.FileTypeChoices.Add("ZIP 压缩包", new List<string> { ".zip" });

        // WinUI 3 的 Picker 必须绑定宿主窗口句柄
        InitializeWithWindow.Initialize(picker, ResolveWindowHandle(xamlRoot));

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    /// <summary>
    /// 打包：每个 MOD 资源包独立下载 + 解压到临时目录，收集其中所有 .jar（按文件名去重）写入 ZIP。
    /// 返回收集到的 jar 数量与 ZIP 路径；异常由调用方转为界面提示。
    /// </summary>
    public static async Task<(int JarCount, string ZipPath)> ExportAsync(
        ServerModInfo info,
        string zipPath,
        Action<string>? progress = null)
    {
        var staging = Path.Combine(Path.GetTempPath(), StagingPrefix + Guid.NewGuid().ToString("N"));
        var jarDir = Path.Combine(staging, "jars");
        Directory.CreateDirectory(jarDir);

        try
        {
            var index = 0;
            foreach (var mod in info.Mods)
            {
                index++;
                if (string.IsNullOrWhiteSpace(mod.Url))
                {
                    continue;
                }

                progress?.Invoke($"正在下载 MOD 包 {index}/{info.Mods.Count}：{mod.Name}");
                var archive = Path.Combine(staging, index + ".7z");
                await DownloadUtil.DownloadAsync(mod.Url, archive).ConfigureAwait(false);

                progress?.Invoke($"正在解压 {mod.Name}…");
                var extractDir = Path.Combine(staging, "x" + index);
                await CompressionUtil.ExtractAsync(archive, extractDir).ConfigureAwait(false);

                // 资源包内 MOD 位于 .minecraft/mods，但为稳妥起见递归收集全部 jar
                foreach (var jar in Directory.EnumerateFiles(extractDir, "*.jar", SearchOption.AllDirectories))
                {
                    var target = Path.Combine(jarDir, Path.GetFileName(jar));
                    if (!File.Exists(target))
                    {
                        File.Copy(jar, target);
                    }
                }
            }

            var jarCount = Directory.GetFiles(jarDir, "*.jar").Length;
            progress?.Invoke($"正在压缩 {jarCount} 个 MOD…");

            // FileSavePicker 会预先创建空文件，ZipFile.CreateFromDirectory 要求目标不存在
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            ZipFile.CreateFromDirectory(jarDir, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            return (jarCount, zipPath);
        }
        finally
        {
            try
            {
                if (Directory.Exists(staging))
                {
                    Directory.Delete(staging, recursive: true);
                }
            }
            catch (Exception)
            {
                // 临时目录清理失败不影响导出结果
            }
        }
    }

    /// <summary>解析宿主窗口句柄：优先 XamlRoot 的内容岛环境，回退到前台窗口（与 ThemeFilePicker 一致）。</summary>
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
            // 内容岛环境不可用（旧系统 / 组件未初始化）：走前台窗口回退
        }

        return GetForegroundWindow();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
