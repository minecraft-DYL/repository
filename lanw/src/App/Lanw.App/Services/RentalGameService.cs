using Lanw.Public.Message;
using Lanw.WPFLauncher.Entities.WPFLauncher.RentalGame;
using Lanw.WPFLauncher.Entities.WPFLauncher.RentalGame.GameCharacters;
using Lanw.WPFLauncher.Protocol;

namespace Lanw.App.Services;

/// <summary>
/// 租赁服列表条目：详情（Message 层缓存分页结果）+ 原始列表实体（补充状态/可见性）。
/// 说明：<see cref="RentalGameMessage.GetServerList"/> 产出的是 <see cref="EntityRentalGameDetails"/>，
/// 该载荷本身不含 status / visibility；状态与可见性只存在于列表接口的 <see cref="EntityRentalGame"/> 上，
/// 故按 EntityId 关联补充。补充失败（无网络 / 接口异常）时 <see cref="Raw"/> 为 null，界面显示「未知」占位，不影响列表主体。
/// </summary>
public sealed record RentalServerEntry(EntityRentalGameDetails Details, EntityRentalGame? Raw)
{
    /// <summary>服务器 ID（entity_id），用于详情页导航与关联。</summary>
    public string EntityId => Details.EntityId;
}

/// <summary>
/// 租赁服数据服务：进程内直调 t16 移植的租赁服协议与消息层
/// （<see cref="RentalGameMessage"/> / <see cref="NPFLauncher"/>），不经过任何 HTTP 中转。
/// 所有远端调用都投递到线程池执行：RentalGameMessage / NPFLauncher 内部是同步等待
/// （GetAwaiter().GetResult()，列表路径还带 Thread.Sleep 节流），直接在 UI 线程调用会阻塞界面。
/// </summary>
public sealed class RentalGameService
{
    /// <summary>
    /// 获取租赁服列表（名称/状态/版本/可见性/在线人数，对应原 Vue rental/GameRental.vue → getRentalServerList）。
    /// 主数据走 Message 层 <see cref="RentalGameMessage.GetServerList"/>（含缓存与分页），
    /// 状态/可见性由列表接口 <see cref="NPFLauncher.GetRentalGameListAsync"/> 按 EntityId 补充。
    /// 首屏（offset = 0）先按在线人数排序，对应原 Vue 的 sortRentalServer()。
    /// </summary>
    /// <param name="offset">偏移量</param>
    /// <param name="pageSize">每页数量（对应原 Vue loadMoreServers(15)）</param>
    public Task<RentalServerEntry[]> GetServerListAsync(int offset = 0, int pageSize = 15)
        => Task.Run(() =>
        {
            if (offset == 0)
            {
                RentalGameMessage.SortServerList(); // 对应 Vue onMounted → sortRentalServer()
            }

            var details = RentalGameMessage.GetServerList(offset, pageSize);
            if (details.Length == 0)
            {
                return [];
            }

            var rawById = LoadRawListById(offset);
            return details
                .Select(detail => new RentalServerEntry(detail, rawById.GetValueOrDefault(detail.EntityId)))
                .ToArray();
        });

    /// <summary>
    /// 获取租赁服详细信息（对应原 Vue rental/GameRentalDetail.vue → getRentalServerDetail）。
    /// </summary>
    /// <param name="entityId">服务器 ID（entity_id）</param>
    public Task<EntityRentalGameDetails> GetServerDetailAsync(string entityId)
        => Task.Run(() => NPFLauncher.GetRentalGameDetailsAsync(entityId).GetAwaiter().GetResult());

    /// <summary>
    /// 获取租赁服连接地址（含移动/电信/联通三线接入地址，对应原 Vue getRentalInfo 里的地址部分）。
    /// </summary>
    /// <param name="serverId">服务器 ID</param>
    /// <param name="password">服务器密码（私有/密码可见服需要；为空时协议按 "none" 处理）</param>
    public Task<EntityRentalGameServerAddress> GetServerAddressAsync(string serverId, string? password = null)
        => Task.Run(() => NPFLauncher.GetGameRentalAddressAsync(serverId, password).GetAwaiter().GetResult());

    /// <summary>
    /// 获取租赁服玩家（游戏角色）列表。接口口径为
    /// <c>/rental-server-player/query/search-by-user-server</c>：当前登录账号在该租赁服上的游戏角色。
    /// </summary>
    /// <param name="serverId">服务器 ID</param>
    public Task<EntityRentalGamePlayerList[]> GetPlayersAsync(string serverId)
        => Task.Run(() => NPFLauncher.GetRentalGameRolesListAsync(serverId).GetAwaiter().GetResult());

    /// <summary>
    /// 拉取列表接口原始实体（用于补充状态/可见性）；失败时返回空表，由界面显示「未知」占位。
    /// </summary>
    private static Dictionary<string, EntityRentalGame> LoadRawListById(int offset)
    {
        try
        {
            return NPFLauncher.GetRentalGameListAsync(offset)
                .GetAwaiter()
                .GetResult()
                .Where(item => !string.IsNullOrWhiteSpace(item.EntityId))
                .GroupBy(item => item.EntityId)
                .ToDictionary(group => group.Key, group => group.First());
        }
        catch (Exception)
        {
            // 无网络 / 接口异常：状态与可见性降级为「未知」，列表主体仍可用
            return [];
        }
    }
}
