using Lanw.Cipher.Cipher.Nirvana.Connection;
using Lanw.Cipher.Yggdrasil;
using Lanw.Core;
using Lanw.Core.Entities;
using Lanw.Core.Manager;
using Lanw.Public.Manager;
using Lanw.Public.Message;
using Lanw.WPFLauncher.Http;
using Lanw.WPFLauncher.Protocol;
using Serilog;

namespace Lanw.Public.Utils;

/// <summary>
/// 启动期初始化（由 Nirvana.Public.Utils.InitProgram 移植）：对应源 NelInit1 + VersionCheck + Online + CreateServices。
/// 桌面版无本地 HTTP：由 App.OnLaunched 在配置初始化（LanwConfig.Initialization）之后直接调用 <see cref="NelInit1"/>，
/// 调用顺序与源保持一致，便于逐条对照。
/// <para>
/// 唯一有意偏差：源在校验失败时 <c>Thread.Sleep(6000); Environment.Exit(1)</c>。桌面版不能因为一次网络抖动
/// 就静默退出（源是“必须联网”的启动器，lanw 要能离线打开设置/日志页），因此：
/// 「连不上服务器」只记错误并继续；「版本已被禁用」仍按源退出（这才是版本安全检测的本意）。
/// </para>
/// </summary>
public static class InitProgram
{
    /// <summary>
    /// 核心初始化（对应源 InitProgram.NelInit1，顺序一致）：
    /// 版本安全检测 → 创建服务（crc_salt）→ 插件管理器初始化 → 版本提示 → 在线检测
    /// → 后台缓存预热（服务器列表）→ 后台提前获取验证服务器 → 命令行开关。
    /// </summary>
    public static void NelInit1(string[] args)
    {
        // 版本安全检测
        VersionCheck();

        // 创建服务
        CreateServices(args);
        Log.Information("------  完成 ------");

        // 插件管理器初始化
        // 对应源注释：避免插件过早加载，因为这是没必要的
        PluginMessage.Initialize();

        for (var i = 0; i < 4 && !LanwProgram.LatestVersion; i++)
        {
            Log.Warning("当前版本不是最新版本，建议更新至最新版本，以获得更好的体验！");
        }

        // 在线检测
        Online();

        // 缓存 服务器/租凭服/皮肤 信息/图片
        _ = Task.Run(() =>
        {
            try
            {
                Thread.Sleep(1000);
                InfoManager.GetToken(); // 是否登录
                CacheManager.CacheServer();
            }
            catch (Exception)
            {
                // ignored（对应源：未登录 / 拉取失败都不影响启动）
            }
        });

        // 提前获取验证服务器
        _ = Task.Run(() => { _ = StandardYggdrasil.InitializationAsync(); });

        foreach (var arg in args)
        {
            if ("--authenticated_false".Equals(arg))
            {
                NetEaseConnection.IsServerAuthenticated = false;
                break;
            }
        }
    }

    /// <summary>
    /// 版本安全检测（对应源 VersionCheck）：非发布版本直接跳过（源：调试版跳过）；
    /// 服务器信息缺失 / 版本列表缺失 / 版本被禁用 均按源报错。启用版本检测前会先补齐服务器信息
    /// （对应源在 NelInit1 之前调用的 FantnelInit → /fantnel.json）。
    /// </summary>
    private static void VersionCheck()
    {
        // 检查是否为发布版本
        if (!LanwProgram.Release)
        {
            Log.Error("调试版，已跳过版本检测！");
            return;
        }

        // 对应源 FantnelInit：NelInit1 之前先把 /fantnel.json 拉入 InfoManager
        if (InfoManager.ServerInfo == null)
        {
            try
            {
                _ = HomeMessage.GetHomeInfoAsync().GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Log.Error("连接服务器失败! 错误信息: {0}", e.Message);
            }
        }

        if (InfoManager.ServerInfo == null)
        {
            // 有意偏差：源在此 Environment.Exit(1)，桌面版仅告警并继续（离线可用）
            Log.Error("无法连接至服务器！已跳过版本检测（离线可用）。");
            return;
        }

        if (InfoManager.ServerInfo.Versions == null)
        {
            Log.Error("检测版本失败，无法检查版本！");
            return;
        }

        var isVersion = false; // 版本 是否存在
        foreach (var version in InfoManager.ServerInfo.Versions)
        {
            if (version == LanwProgram.Version)
            {
                isVersion = true;
            }
        }

        if (!isVersion)
        {
            Log.Error("该版本已被禁用，请前往 https://npyyds.top/ 查看最新版本！");
            Thread.Sleep(6000);
            Environment.Exit(1);
        }

        // 检查是否为最新版本（注意：源把“有新版本”记作 LatestVersion = true，
        // 而 lanw 的 LanwProgram.LatestVersion 语义为“当前就是最新版”，此处按 lanw 既有语义取反。）
        if (InfoManager.ServerInfo.Versions.Last().Equals(LanwProgram.Version))
        {
            return;
        }

        LanwProgram.LatestVersion = false;
    }

    /// <summary>创建服务（对应源 CreateServices）：注入 crc_salt（命令行优先，其次服务器下发）。</summary>
    private static void CreateServices(string[] args)
    {
        X19.CrcSalt = RestartTools.Get("crc_salt", args);
        if (string.IsNullOrEmpty(X19.CrcSalt) && InfoManager.ServerInfo != null)
        {
            X19.CrcSalt = InfoManager.ServerInfo.CrcSalt;
        }

        if (X19.CrcSalt != null && X19.CrcSalt.Length > 6)
        {
            Log.Information("CRC Salt 计算完成: {0}....", X19.CrcSalt[..6]);
        }
    }

    /// <summary>Fantnel 在线检测（对应源 Online）：每 180 秒上报一次运行环境（系统/架构/版本），异常只告警。</summary>
    private static async void Online()
    {
        try
        {
            while (true)
            {
                try
                {
                    // 60 * 3 = 180 秒（3 分钟）
                    for (var i = 0; i < 180; i++)
                    {
                        await Task.Delay(1000);
                    }

                    await X19Extensions.Nirvana.ApiAsync<EntityResponse<string>>("/api/tick?mode=fantnel", new Dictionary<string, string>
                    {
                        { "system", LanwProgram.Mode },
                        { "arch", LanwProgram.Arch },
                        { "version", LanwProgram.Version },
                        { "versionId", LanwProgram.VersionId.ToString() }
                    });
                }
                catch (Exception e)
                {
                    Log.Warning(" 在线检测异常! 错误信息: {0}", e.Message);
                }
            }
        }
        catch (Exception e)
        {
            Log.Warning(" 在线检测出错! 错误信息: {0}", e.Message);
        }
    }
}
