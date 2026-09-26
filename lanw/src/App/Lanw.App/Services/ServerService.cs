using Lanw.Public.Entities.NEL;
using Lanw.Public.Message;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame;

namespace Lanw.App.Services;

/// <summary>
/// 网络服数据服务：进程内直调 t14 移植的网络服协议（Lanw.Public.ServersGameMessage /
/// Lanw.WPFLauncher.NPFLauncher），不经过任何 HTTP 中转。
/// 所有远端调用都投递到线程池执行：ServersGameMessage / EntityServerDetail 内部是同步等待
/// （GetAwaiter().GetResult()），直接在 UI 线程调用会阻塞界面，故必须切到后台线程。
/// </summary>
public sealed class ServerService
{
    /// <summary>
    /// 获取网络服列表（缓存分页，对应原 Vue Servers.vue → /api/gameserver/get）。
    /// </summary>
    /// <param name="offset">偏移量</param>
    /// <param name="pageSize">每页数量</param>
    /// <param name="version">按 MC 版本过滤（空串表示不过滤）</param>
    public Task<EntityNetGameItem[]> GetServerListAsync(int offset = 0, int pageSize = 15, string version = "")
        => Task.Run(() => ServersGameMessage.GetServerListTo(offset, pageSize, true, version));

    /// <summary>
    /// 获取网络服详情（详情 + 服务器地址并行聚合，对应原 Vue ServerDetail.vue → /api/gameserver/id）。
    /// </summary>
    /// <param name="serverId">服务器 ID（entity_id）</param>
    public Task<EntityServerDetail> GetServerDetailAsync(string serverId)
        => Task.Run(() => new EntityServerDetail(serverId));
}
