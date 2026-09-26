using Lanw.Core.Entities;
using Lanw.Core.Utils.CodeTools;
using Lanw.WPFLauncher.Entities;
using Lanw.WPFLauncher.Entities.WPFLauncher;
using Lanw.WPFLauncher.Entities.WPFLauncher.Minecraft;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameLaunch.Texture;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameSkin;
using Lanw.WPFLauncher.Http;

namespace Lanw.WPFLauncher.Protocol;

// ReSharper disable once InconsistentNaming
/// <summary>
/// X19 启动器协议——皮肤/贴图端点（NPFLauncher 分部文件，由
/// Nirvana.WPFLauncher.Protocol.NPFLauncher 皮肤相关方法移植，自研）。
/// 含：免费皮肤列表 / 按名称查询 / 皮肤详情 / 设置皮肤 / 用户游戏贴图查询，
/// 以及为“上传本地皮肤”功能自研设计的上传端点（参考源无此端点）。
/// </summary>
public static partial class NPFLauncher
{
    /// <summary>
    /// 获取皮肤详情（由 Nirvana.WPFLauncher.Protocol.NPFLauncher.GetSkinDetailsAsync(List<string>) 移植，自研）。
    /// @param skinList 皮肤ID列表
    /// @return 皮肤详情
    /// </summary>
    private static async Task<EntityQueryNetSkinItem[]> GetSkinDetailsAsync(List<string> skinList)
    {
        var entity = await X19Extensions.Gateway.ApiAsync<EntitiesWPFLauncher<EntityQueryNetSkinItem>>("/item/query/search-by-ids", new EntitySkinDetailsRequest
        {
            ChannelId = 11,
            EntityIds = skinList,
            IsHas = true,
            WithPrice = true,
            WithTitleImage = true
        });
        return entity == null ? throw new ErrorCodeException(ErrorCode.NotFound) : entity.SafeEntity();
    }

    /// <summary>
    /// 获取皮肤信息（由 Nirvana.WPFLauncher.Protocol.NPFLauncher.GetSkinDetailsAsync(string) 移植，自研）。
    /// @param skinId 皮肤ID
    /// @return 皮肤信息
    /// </summary>
    public static async Task<EntityQueryNetSkinItem> GetSkinDetailsAsync(string skinId)
    {
        return (await GetSkinDetailsAsync([skinId]))[0];
    }

    /// <summary>
    /// 设置皮肤（由 Nirvana.WPFLauncher.Protocol.NPFLauncher.SetSkinAsync 移植，自研）。
    /// 对单/网络/租赁/本地联机/大厅各游戏类型统一应用皮肤。
    /// @param entityId 皮肤ID
    /// @return 操作结果
    /// </summary>
    public static async Task<EntityWPFResponse?> SetSkinAsync(string entityId)
    {
        var entity = await X19Extensions.Gateway.ApiAsync<EntityWPFResponse>("/user-game-skin-multi", new
        {
            skin_settings = new List<EntitySkinSettings>
            {
                new()
                {
                    ClientType = "java",
                    GameType = 9,
                    SkinId = entityId,
                    SkinMode = 0,
                    SkinType = 31
                },
                new()
                {
                    ClientType = "java",
                    GameType = 8,
                    SkinId = entityId,
                    SkinMode = 0,
                    SkinType = 31
                },
                new()
                {
                    ClientType = "java",
                    GameType = 2,
                    SkinId = entityId,
                    SkinMode = 0,
                    SkinType = 31
                },
                new()
                {
                    ClientType = "java",
                    GameType = 10,
                    SkinId = entityId,
                    SkinMode = 0,
                    SkinType = 31
                },
                new()
                {
                    ClientType = "java",
                    GameType = 7,
                    SkinId = entityId,
                    SkinMode = 0,
                    SkinType = 31
                }
            }
        });
        if (entity == null)
        {
            throw new ErrorCodeException();
        }

        return entity.Code != 0 ? throw new EntityX19Exception(entity.Message, entity) : entity;
    }

    /// <summary>
    /// 按名称查询免费皮肤列表（由 Nirvana.WPFLauncher.Protocol.NPFLauncher.GetFreeSkinByNameAsync 移植，自研）。
    /// @param name 皮肤名称
    /// @param offset 偏移量
    /// @param pageSize 数量
    /// @return 皮肤列表
    /// </summary>
    public static async Task<EntityQueryNetSkinItem[]> GetFreeSkinByNameAsync(string name, int offset = 0, int pageSize = 10)
    {
        var entity = await X19Extensions.Gateway.ApiAsync<EntitiesWPFLauncher<EntityQueryNetSkinItem>>("/item/query/search-by-keyword", new EntityQuerySkinByNameRequest
        {
            IsHas = true,
            IsSync = 0,
            ItemType = 2,
            Keyword = name,
            Length = pageSize,
            MasterTypeId = 10,
            Offset = offset,
            PriceType = 3,
            SecondaryTypeId = "31",
            SortType = 1,
            Year = 0
        });
        if (entity == null)
        {
            throw new ErrorCodeException();
        }

        return entity.Code != 0 ? throw new EntityX19Exception(entity.Message, entity) : entity.SafeEntity();
    }

    /// <summary>
    /// 获取用户游戏皮肤（由 Nirvana.WPFLauncher.Protocol.NPFLauncher.GetSkinListInGameAAsync 移植，自研）。
    /// 按用户/游戏类型查询其正在使用的游戏贴图（进游戏皮肤同步使用）。
    /// @param userGame 用户游戏贴图查询请求
    /// @return 用户游戏贴图列表
    /// </summary>
    public static async Task<EntityUserGameTexture[]?> GetSkinListInGameAAsync(EntityUserGameTextureRequest userGame)
    {
        var entity = await X19Extensions.Gateway.ApiAsync<EntitiesWPFLauncher<EntityUserGameTexture>>("/user-game-skin/query/search-by-type", userGame);
        return entity?.Data;
    }

    /// <summary>
    /// 上传本地皮肤（lanw 自研设计：参考源 Fantnel/Nirvana 无“上传本地皮肤”端点，
    /// 为皮肤页“上传本地皮肤”功能扩展。皮肤 PNG 以 Base64 JSON 提交到 /user-game-skin，
    /// 上传成功返回新的用户游戏贴图实体，可再经 SetSkinAsync 应用）。
    /// @param skinPng 本地皮肤 PNG 字节
    /// @param fileName 文件名（含扩展名）
    /// @param clientType 客户端类型（默认 Java）
    /// @param skinType 贴图类型（默认普通皮肤）
    /// @param skinMode 皮肤模式（0=经典 1=纤细）
    /// @return 上传后的用户游戏贴图
    /// </summary>
    public static async Task<EntityUserGameTexture> UploadSkinAsync(byte[] skinPng, string fileName = "skin.png", EnumGameClientType clientType = EnumGameClientType.Java, EnumTextureType skinType = EnumTextureType.SKIN, int skinMode = 0)
    {
        var entity = await X19Extensions.Gateway.ApiAsync<EntityWPFLauncher<EntityUserGameTexture>>("/user-game-skin", new EntityUploadSkinRequest
        {
            File = Convert.ToBase64String(skinPng),
            FileName = fileName,
            ClientType = clientType,
            SkinType = skinType,
            SkinMode = skinMode
        });
        if (entity == null)
        {
            throw new ErrorCodeException();
        }

        return entity.Code != 0 ? throw new EntityX19Exception(entity.Message, entity) : entity.SafeEntity();
    }
}
