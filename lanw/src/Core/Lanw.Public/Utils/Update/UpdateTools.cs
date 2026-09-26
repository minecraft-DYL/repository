using System.Runtime.InteropServices;
using Lanw.Core;
using Lanw.Core.Manager;
using Lanw.Core.Utils;
using Lanw.Public.Entities.Update;
using Serilog;

namespace Lanw.Public.Utils.Update;

/// <summary>
/// 更新检查编排（由 Nirvana.Public.Utils.Update.UpdateTools 移植）。
/// </summary>
public static class UpdateTools {
    
    // 检查更新
    public static void CheckUpdate(string[] args)
    {
        CheckUpdateAsync(args).GetAwaiter().GetResult();
    }

    // 检查更新
    private static async Task CheckUpdateAsync(string[] args)
    {
        if (InfoManager.ServerInfo == null) {
            Log.Error("无法连接至服务器！");
            Thread.Sleep(6000);
            Environment.Exit(1);
            return;
        }

        if (!LanwProgram.UpdateVersion.Equals(InfoManager.ServerInfo.UpdateVersions)) {
            Log.Error("当前版本已被禁用，请前往官网重新下载！");
            Thread.Sleep(6000);
            Environment.Exit(1);
            return;
        }

        // --- Fantnel ---
        var update = 0; // 0:正常检查 1:不检查 2:已被检查
        if (args.Any(arg => arg == "--update_false")) {
            update = 2;
        }

        // 正常检查
        if (update == 0 && LanwProgram.Release) {
            await new EntityUpdate {
                Mode = PathUtil.SystemArch,
                Name = "Fantnel",
                SafeMode = true,
                Command = ""
            }.CheckUpdateSafe();
        }

        // --- Fantnel UI ---
        update = 0; // 0:正常检查 1:不检查 2:已被检查
        if (args.Any(arg => arg == "--update_ui_false")) {
            update = 2;
        }

        if (update == 0) {
            await new EntityUpdate {
                Mode = "ui." + ConfigUtil.GetConfig("themeValue", RestartTools.Get("default_skin_id", args, "nirvana")),
                Name = "Fantnel UI"
            }.CheckUpdateSafe();
        }

        // --- Static ---
        update = 0; // 0:正常检查 1:不检查 2:已被检查
        if (args.Any(arg => arg == "--update_static_false")) {
            update = 2;
        }

        if (update == 0) {
            await new EntityUpdate {
                Mode = "static"
            }.CheckUpdateSafe();
        }

        // --- Static System ---
        update = 0; // 0:正常检查 1:不检查 2:已被检查
        if (args.Any(arg => arg == "--update_static_system_false")) {
            update = 2;
        }

        if (update == 0) {
            await new EntityUpdate {
                Mode = "static." + PathUtil.DetectOperating,
                Name = "Resource System"
            }.CheckUpdateSafe();
        }

        // --- Static Linux System ---
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            update = 0; // 0:正常检查 1:不检查 2:已被检查
            if (args.Any(arg => arg == "--update_static_linux_system_false")) {
                update = 2;
            }

            if (update == 0) {
                await new EntityUpdate {
                    Mode = "static." + PathUtil.SystemArch,
                    Name = "Resource Linux"
                }.CheckUpdateSafe();
            }
        }
    }
}
