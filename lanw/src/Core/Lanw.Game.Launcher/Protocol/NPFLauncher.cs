using Lanw.Core.Utils.CodeTools;
using Lanw.Game.Launcher.Entities.WPFLauncher.Minecraft;
using Lanw.Game.Launcher.Entities.WPFLauncher.Minecraft.Mods;
using Lanw.Game.Launcher.Entities.WPFLauncher.NetGame.GameLaunch.GameMods;
using Lanw.Game.Launcher.Entities.WPFLauncher.NetGame.GameLaunch.Texture;
using Lanw.Game.Launcher.Utils;
using Lanw.WPFLauncher.Entities;
using Lanw.WPFLauncher.Entities.WPFLauncher;
using Lanw.WPFLauncher.Http;

namespace Lanw.Game.Launcher.Protocol;

/// <summary>
/// 启动流程所需的 X19 协议访问桥接（由 Nirvana.WPFLauncher.Protocol.NPFLauncher 的对应方法移植）。
/// 说明：这些方法在参考源中位于 WPFLauncher 工程，当前的 Lanw.WPFLauncher 尚未移植到这一层
/// （网络服/皮肤协议由并行任务补充）。为避免与并行任务重复定义，先在本工程内以同签名桥接，
/// 待 Lanw.WPFLauncher.Protocol.NPFLauncher 补齐后可直接替换调用方。
/// </summary>
// ReSharper disable once InconsistentNaming
public static class NPFLauncher {
    /**
     * 获取 白端基础 模组
     */
    public static async Task<EntityQuerySearchByGameResponse?> GetGameCoreModListAsync(EnumGameVersion gameVersion, bool isRental)
    {
        var entity = await X19Extensions.Gateway.ApiAsync<EntityWPFLauncher<EntityQuerySearchByGameResponse>>("/game-auth-item-list/query/search-by-game", new EntityQuerySearchByGameRequest {
            McVersionId = (int)gameVersion,
            GameType = isRental ? 8 : 2
        });
        return entity == null ? throw new ErrorCodeException(ErrorCode.DetailError) : entity.SafeEntity();
    }

    /**
     * 获取 白端模组 详细信息
     */
    public static async Task<EntityComponentDownloadInfoResponse[]> GetGameCoreModDetailsListAsync(List<ulong> gameModList)
    {
        var entity = await X19Extensions.Gateway.ApiAsync<EntitiesWPFLauncher<EntityComponentDownloadInfoResponse>>("/user-item-download-v2/get-list", new EntitySearchByIdsQuery {
            ItemIdList = gameModList
        });
        if (entity == null) {
            throw new ErrorCodeException();
        }

        return entity.Code != 0 ? throw new EntityX19Exception(entity.Message, entity) : entity.SafeEntity();
    }

    /**
     * 获取 白端服务器 模组
     */
    private static async Task<EntityWPFLauncher<EntityComponentDownloadInfoResponse>?> GetNetGameComponentDownloadListBAsync(string serverId)
    {
        var entity = await X19Extensions.Client.ApiAsync<EntityWPFLauncher<EntityComponentDownloadInfoResponse>>("/user-item-download-v2", new EntitySearchByItemIdQuery {
            ItemId = serverId,
            Length = 0,
            Offset = 0
        });
        return entity;
    }

    /**
     * 获取 白端服务器 模组
     */
    public static async Task<EntityComponentDownloadInfoResponse?> GetNetGameComponentDownloadListAAsync(string serverId)
    {
        var entity = await GetNetGameComponentDownloadListBAsync(serverId);
        return entity?.Data;
    }

    public static async Task<EntityComponentDownloadInfoResponse> GetNetGameComponentDownloadListAsync(string gameId)
    {
        var entity = await GetNetGameComponentDownloadListBAsync(gameId);
        return entity == null ? throw new ErrorCodeException() : entity.SafeEntity();
    }

    /**
     * 获取 白端依赖
     */
    public static async Task<EntityCoreLibResponse> GetMinecraftClientLibsAsync(EnumGameVersion? gameVersion = null)
    {
        uint gameVersionId = 0;
        if (gameVersion != null) {
            gameVersionId = (uint)gameVersion.Value;
        }

        var entity = await X19Extensions.Client.ApiAsync<EntityWPFLauncher<EntityCoreLibResponse>>("/game-patch-info", new EntityMcDownloadVersion {
            McVersion = gameVersionId
        });
        return entity == null ? throw new ErrorCodeException() : entity.SafeEntity();
    }

    /**
     * 获取游戏皮肤
     */
    public static async Task<EntityUserGameTexture[]?> GetSkinListInGameAAsync(EntityUserGameTextureRequest userGame)
    {
        var entity = await X19Extensions.Gateway.ApiAsync<EntitiesWPFLauncher<EntityUserGameTexture>>("/user-game-skin/query/search-by-type", userGame);
        return entity?.Data;
    }
}
