using System.Threading;
using Lanw.Core.Utils.CodeTools;
using Lanw.WPFLauncher.Entities.WPFLauncher.RentalGame;
using Lanw.WPFLauncher.Entities.WPFLauncher.RentalGame.GameCharacters;
using Lanw.WPFLauncher.Protocol;
using Serilog;

namespace Lanw.Public.Message;

/// <summary>
/// 租赁服消息编排（由 Nirvana.Public.Message.RentalGameMessage 移植，自研）。
/// 负责租赁服服务器列表的拉取/去重/分页缓存与指定游戏角色查询。
/// 注：原参考在拉取列表时调用 CacheManager（DownloadCacheImage / GetCacheImageUrl）
/// 做图片本地缓存，该缓存引擎属网络服功能模块（t14 尚未移植），此处省略相应副作用调用，
/// 图片 URL 保持远端原始地址，后续缓存模块落地后可再行接入。
/// </summary>
public static class RentalGameMessage
{
    // 服务器列表[普通信息] - 缓存
    public static Dictionary<string, EntityRentalGameDetails> ServerList = [];

    /// <summary>
    /// 获取服务器列表[普通信息]
    /// @param offset 偏移量
    /// @param pageSize 每页数量
    /// @return 服务器列表[普通信息]
    /// </summary>
    public static EntityRentalGameDetails[] GetServerList(int offset = 0, int pageSize = 10)
    {
        return GetServerListAsync(offset, pageSize).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 获取服务器列表[普通信息]
    /// @param offset 偏移量
    /// @param pageSize 每页数量
    /// @return 服务器列表[普通信息]
    /// </summary>
    private static async Task<EntityRentalGameDetails[]> GetServerListAsync(int offset = 0, int pageSize = 10)
    {
        var index = -pageSize; // 循环次数
        var count = offset + pageSize;

        while (true)
        {
            // 缓存图片下载：CacheManager.DownloadCacheImage()（缓存引擎未移植，省略，见类注释）

            // ServerList 有 就用缓存
            // 分页
            if (ServerList.Count >= count)
            {
                var list = ServerList.Skip(offset).Take(pageSize).ToArray();
                return GetServerList(list);
            }

            if (++index > 0)
            {
                // 最后一页, 减少数量，避免丢失数据
                count--;
                pageSize--;
                if (pageSize <= 0)
                {
                    return [];
                }
            }
            else
            {
                var items = await NPFLauncher.GetRentalGameListAsync();
                AddServerList(items);
                Thread.Sleep(560);
            }
        }
    }

    /// <summary>
    /// 排序 按 PlayerCount 高到低
    /// </summary>
    public static void SortServerList()
    {
        ServerList = ServerList.OrderByDescending(x => x.Value.PlayerCount).ToDictionary(x => x.Key, x => x.Value);
    }

    private static EntityRentalGameDetails[] GetServerList(KeyValuePair<string, EntityRentalGameDetails>[] serverList)
    {
        return serverList.Select(x => x.Value).ToArray();
    }

    // 服务器列表[普通信息] - 添加
    private static void AddServerList(EntityRentalGameDetails gameItem)
    {
        if (ServerList.Any(item => item.Value.EntityId == gameItem.EntityId))
        {
            return;
        }

        // 图片缓存：CacheManager.GetCacheImageUrl(gameItem)（缓存引擎未移植，省略，见类注释）
        ServerList.Add(gameItem.EntityId, gameItem);
    }

    private static void AddServerList(EntityRentalGame[] gameItem)
    {
        foreach (var item in gameItem)
        {
            var details = NPFLauncher.GetRentalGameDetailsAsync(item.EntityId).GetAwaiter().GetResult();
            AddServerList(details);
        }
    }

    /// <summary>
    /// 获取服务器上的指定游戏角色
    /// @param serverId 服务器ID
    /// @param name 游戏角色名称
    /// @return 服务器上的指定游戏角色
    /// </summary>
    public static EntityRentalGamePlayerList GetUserName(string serverId, string name)
    {
        return GetUserNameAsync(serverId, name).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 获取服务器上的指定游戏角色
    /// @param serverId 服务器ID
    /// @param name 游戏角色名称
    /// @return 服务器上的指定游戏角色
    /// </summary>
    public static async Task<EntityRentalGamePlayerList> GetUserNameAsync(string serverId, string name)
    {
        for (var i = 0; i < 3; i++)
        {
            try
            {
                var games = await NPFLauncher.GetRentalGameRolesListAsync(serverId);
                if (games == null)
                {
                    throw new ErrorCodeException(ErrorCode.NotFound);
                }

                foreach (var game in games)
                {
                    if (game.Name == name)
                    {
                        return game;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("获取游戏角色 {0} 失败 {1}", name, e.Message);
            }

            Thread.Sleep(800);
        }

        throw new ErrorCodeException(ErrorCode.NotFoundName);
    }
}
