using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Lanw.Core.Utils;
using Lanw.Public.Entities.Update;
using Lanw.WPFLauncher.Http;

namespace Lanw.App.Services;

/// <summary>更新清单中的单个文件（只读展示用）。</summary>
/// <param name="Path">清单内相对路径。</param>
/// <param name="LocalPath">本地落地路径（经 t17 EntityUpdateFile.GetPath 计算）。</param>
/// <param name="Size">清单声明的大小（字节）。</param>
/// <param name="Sha256">清单声明的 SHA256。</param>
/// <param name="Url">下载地址。</param>
/// <param name="LocalExists">本地文件是否存在。</param>
/// <param name="LocalSizeMatches">本地大小是否与清单一致（本地不存在时为 null）。</param>
public sealed record UpdateFileItem(
    string Path,
    string LocalPath,
    long? Size,
    string? Sha256,
    string? Url,
    bool LocalExists,
    bool? LocalSizeMatches);

/// <summary>单个更新模式（win.x64 / ui.xxx / static …）的检查结果。</summary>
public sealed class UpdateModeResult
{
    public required string Mode { get; init; }

    public required string Name { get; init; }

    public required bool Success { get; init; }

    public required string Message { get; init; }

    public required IReadOnlyList<UpdateFileItem> Files { get; init; }
}

/// <summary>
/// 版本检查服务（对应原版 /api/version 与 UpdateTools 的更新清单来源）。
/// 说明（重要）：UpdateTools / <see cref="EntityUpdate"/> 的控制台语义会
/// <c>Environment.Exit</c>（失败退出、安全模式下载后重启退出），桌面 GUI 下直接调用会杀掉应用，
/// 故此处只做「只读」检查：用与 <c>EntityUpdate.Initialize</c> 完全相同的端点与解析方式取清单，
/// 并复用 t17 的 <see cref="EntityUpdate"/>（模式描述）与 <see cref="EntityUpdateFile"/>（本地路径换算）来展示，
/// 不下载、不写盘、不退出进程。
/// </summary>
public static class VersionService
{
    /// <summary>构建与 <see cref="UpdateTools.CheckUpdate"/> 相同的更新目标清单。</summary>
    public static IReadOnlyList<EntityUpdate> BuildUpdateTargets()
    {
        // 与 UpdateTools.CheckUpdate 的五路检查保持一致（Linux 专有项仅在 Linux 上出现）
        var targets = new List<EntityUpdate>
        {
            new()
            {
                Mode = PathUtil.SystemArch,
                Name = "Fantnel",
                SafeMode = true,
                Command = ""
            },
            new()
            {
                Mode = "ui." + ConfigUtil.GetConfig("themeValue", "nirvana"),
                Name = "Fantnel UI"
            },
            new()
            {
                Mode = "static"
            },
            new()
            {
                Mode = "static." + PathUtil.DetectOperating,
                Name = "Resource System"
            }
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            targets.Add(new EntityUpdate
            {
                Mode = "static." + PathUtil.SystemArch,
                Name = "Resource Linux"
            });
        }

        return targets;
    }

    /// <summary>依次检查全部更新目标（只读）。</summary>
    public static async Task<IReadOnlyList<UpdateModeResult>> CheckAllAsync()
    {
        var results = new List<UpdateModeResult>();
        foreach (var target in BuildUpdateTargets())
        {
            results.Add(await CheckAsync(target).ConfigureAwait(true));
        }

        return results;
    }

    /// <summary>检查单个更新模式（只读：获取远端清单 + 比对本地文件是否缺失/大小不同）。</summary>
    public static async Task<UpdateModeResult> CheckAsync(EntityUpdate update)
    {
        try
        {
            // 与参考源 EntityUpdate.Initialize 相同的端点与解析
            var jsonObj = await X19Extensions.Nirvana.ApiAsync<JsonObject>($"/api/fantnel/update/get?mode={update.Mode}");
            if (jsonObj == null)
            {
                return Failure(update, "获取更新信息失败，请检查网络连接。");
            }

            var data = jsonObj["data"];
            if (data == null)
            {
                return Failure(update, "获取更新信息出错，请检查网络连接。");
            }

            var jsonArray = data.AsArray();
            var files = new List<UpdateFileItem>();
            for (var i = 0; i < jsonArray.Count; i++)
            {
                files.Add(ToFileItem(jsonArray[i], i));
            }

            return new UpdateModeResult
            {
                Mode = update.Mode,
                Name = update.Name,
                Success = true,
                Message = files.Count == 0 ? "已是最新版本（无需更新）" : $"远端共 {files.Count} 个文件",
                Files = files,
            };
        }
        catch (Exception e)
        {
            return Failure(update, "检查失败：" + e.Message);
        }
    }

    private static UpdateModeResult Failure(EntityUpdate update, string message)
    {
        return new UpdateModeResult
        {
            Mode = update.Mode,
            Name = update.Name,
            Success = false,
            Message = message,
            Files = [],
        };
    }

    private static UpdateFileItem ToFileItem(JsonNode? node, int index)
    {
        // 复用 t17 的实体做本地路径换算（与真实更新流程完全同源）
        var entity = new EntityUpdateFile(node)
        {
            Index = index
        };

        var localPath = entity.GetPath(false) ?? string.Empty;
        var exists = !string.IsNullOrEmpty(localPath) && File.Exists(localPath);

        long? size = null;
        var sizeNode = node?["size"];
        if (sizeNode != null)
        {
            size = sizeNode.GetValue<long>();
        }

        bool? sizeMatches = null;
        if (exists && size != null)
        {
            try
            {
                sizeMatches = new FileInfo(localPath).Length == size.Value;
            }
            catch (Exception)
            {
                sizeMatches = null;
            }
        }

        return new UpdateFileItem(
            Path: string.IsNullOrEmpty(localPath) ? "(无路径)" : RelativeOf(localPath),
            LocalPath: localPath,
            Size: size,
            Sha256: node?["sha256"]?.GetValue<string>(),
            Url: node?["url"]?.GetValue<string>(),
            LocalExists: exists,
            LocalSizeMatches: sizeMatches);
    }

    /// <summary>只展示清单内的相对路径（本地绝对路径仅用于提示，不铺满界面）。</summary>
    private static string RelativeOf(string localPath)
    {
        if (string.IsNullOrEmpty(localPath))
        {
            return "(无路径)";
        }

        var relative = System.IO.Path.GetRelativePath(PathUtil.UpdaterBasePath, localPath);
        return string.IsNullOrEmpty(relative) ? localPath : relative;
    }
}
