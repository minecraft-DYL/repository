using Lanw.Core.Entities;
using Lanw.Core.Manager;
using Lanw.WPFLauncher.Http;
using Lanw.WPFLauncher.Protocol;
using Serilog;

namespace Lanw.Public.Message;

/// <summary>
/// 主页信息编排（内嵌等价：Fantnel.Servlet OthersController 的 GET /api/home +
/// Nirvana.Public InitProgram 的 FantnelInit 拉取 /fantnel.json + CreateServices 的 CrcSalt 填充）。
/// 原服务器启动时从涅槃服务器拉取 fantnel.json 缓存到 InfoManager，前端 /api/home 读取缓存；
/// 桌面版无本地 HTTP，直接内嵌"拉取-缓存-读取"链路。
/// 与原版差异：原版初始化失败会 Environment.Exit(1)，桌面版返回 null 由 UI 提示。
/// </summary>
public static class HomeMessage
{
    /// <summary>
    /// 获取主页信息（公告 ad1/ad2/ad3 + crcSalt + 版本列表）。
    /// 首次调用从远端拉取并缓存到 InfoManager.ServerInfo，后续直接读缓存。
    /// </summary>
    /// <returns>服务器信息实体；远端不可达时返回 null</returns>
    public static async Task<EntityInfo?> GetHomeInfoAsync()
    {
        if (InfoManager.ServerInfo != null)
        {
            return InfoManager.ServerInfo;
        }

        // 原版 FantnelInitAsync 重试 3 次
        for (var i = 0; i < 3; i++)
        {
            try
            {
                var entity = await X19Extensions.Nirvana.ApiAsync<EntityInfo>("/fantnel.json").ConfigureAwait(false);
                if (entity != null)
                {
                    InfoManager.ServerInfo = entity;

                    // 对应原版 CreateServices：CrcSalt 未设置时由服务器信息下发
                    if (string.IsNullOrEmpty(X19.CrcSalt))
                    {
                        X19.CrcSalt = entity.CrcSalt;
                    }

                    return entity;
                }
            }
            catch (Exception e)
            {
                Log.Error("连接服务器失败! 错误信息: {0}", e.Message);
            }
        }

        Log.Error("连接服务器失败!");
        return null;
    }

    /// <summary>
    /// 获取最新盒子版本号（X19.GameVersion：网易 patchlist 解析）。
    /// X19.GameVersion 为同步网络调用，此包装放到线程池执行避免冻结 UI。
    /// </summary>
    /// <returns>版本号字符串；获取失败返回 null</returns>
    public static Task<string?> GetGameVersionAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                return X19.GameVersion;
            }
            catch (Exception e)
            {
                Log.Error("获取最新版本失败! 错误信息: {0}", e.Message);
                return null;
            }
        });
    }
}
