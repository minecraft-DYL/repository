using Lanw.Core.Entities;
using Lanw.Public.Entities.NEL;
using Lanw.Public.Message;
using Lanw.WPFLauncher.Entities.WPFLauncher.Minecraft;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameLaunch.Texture;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameSkin;
using Lanw.WPFLauncher.Protocol;

namespace Lanw.App.Services;

/// <summary>
/// 皮肤数据服务：进程内直调 t15 移植的皮肤协议（Lanw.Public.SkinMessage / EntitySkinDetail /
/// Lanw.WPFLauncher.NPFLauncher 皮肤端点），不经过任何 HTTP 中转。
/// 所有远端调用都投递到线程池：SkinMessage.GetSkinList 内部含分页拉取 + Thread.Sleep(500) 节流、
/// GetSkinListByName 内部是 GetAwaiter().GetResult() 同步等待、EntitySkinDetail 构造函数同步阻塞，
/// 直接在 UI 线程调用会卡死界面，故统一 Task.Run 包裹。
/// </summary>
public sealed class SkinService
{
    /// <summary>
    /// 列表累计上限（对应原 Vue skin/Skins.vue 的 <c>offset &gt;= 150</c> 终止条件）：
    /// 连续分页拉取到 150 条后停止自动加载，避免无休止请求。
    /// </summary>
    public const int MaxListItems = 150;

    /// <summary>
    /// 每页数量（对应原 Vue fetchSkins(15) / fetchSearchSkins(15)）。
    /// </summary>
    public const int PageSize = 15;

    /// <summary>
    /// 应用皮肤时会一并写入的游戏类型（与 <see cref="NPFLauncher.SetSkinAsync"/> 内的
    /// 5 组 <see cref="EntitySkinSettings"/> 一致：单机 9 / 网络服 8 / 租赁服 2 / 本地联机 10 / 大厅 7）。
    /// 仅用于页面展示「应用设置」，不参与请求构造。
    /// </summary>
    public static readonly IReadOnlyList<string> AppliedGameTypes =
    [
        "单机（game_type 9）",
        "网络服（game_type 8）",
        "租赁服（game_type 2）",
        "本地联机（game_type 10）",
        "大厅（game_type 7）",
    ];

    /// <summary>
    /// 获取皮肤列表（缓存分页，对应原 Vue getGameSkinList → SkinMessage.GetSkinList）。
    /// </summary>
    /// <param name="offset">偏移量</param>
    /// <param name="pageSize">每页数量</param>
    /// <param name="safeImage">是否对缺图条目从详情页补图</param>
    public Task<EntityQueryNetSkinItem[]> GetSkinListAsync(int offset = 0, int pageSize = PageSize, bool safeImage = true)
        => Task.Run(() => SkinMessage.GetSkinList(offset, pageSize, safeImage));

    /// <summary>
    /// 按名称搜索皮肤（对应原 Vue getGameSkinListByName → SkinMessage.GetSkinListByName）。
    /// </summary>
    /// <param name="name">搜索关键词</param>
    /// <param name="offset">偏移量</param>
    /// <param name="pageSize">每页数量</param>
    public Task<EntityQueryNetSkinItem[]> SearchSkinAsync(string name, int offset = 0, int pageSize = PageSize)
        => Task.Run(() => SkinMessage.GetSkinListByName(name, offset, pageSize));

    /// <summary>
    /// 获取皮肤详情（对应原 Vue getGameSkinDetail → EntitySkinDetail）。
    /// </summary>
    /// <param name="skinId">皮肤 ID（entity_id）</param>
    public Task<EntitySkinDetail> GetSkinDetailAsync(string skinId)
        => Task.Run(() => new EntitySkinDetail(skinId));

    /// <summary>
    /// 应用皮肤（对应原 Vue setGameSkin → NPFLauncher.SetSkinAsync）：
    /// 按 5 组 EntitySkinSettings（skin_type=31 / client_type=java）统一写入各游戏类型。
    /// </summary>
    /// <param name="skinId">皮肤 ID（entity_id）</param>
    public Task<EntityWPFResponse?> ApplySkinAsync(string skinId)
        => Task.Run(() => NPFLauncher.SetSkinAsync(skinId));

    /// <summary>
    /// 查询当前账号在用的用户游戏贴图（Texture 协议 /user-game-skin/query/search-by-type）。
    /// </summary>
    /// <param name="request">贴图查询请求</param>
    public Task<EntityUserGameTexture[]> GetUserGameTexturesAsync(EntityUserGameTextureRequest request)
        => Task.Run(async () => await NPFLauncher.GetSkinListInGameAAsync(request).ConfigureAwait(false) ?? []);

    /// <summary>
    /// 上传本地皮肤（lanw 自研设计：Texture 协议 /user-game-skin，PNG 以 Base64 JSON 提交）。
    /// </summary>
    /// <param name="pngBytes">皮肤 PNG 字节</param>
    /// <param name="fileName">文件名（含扩展名）</param>
    /// <param name="skinMode">皮肤模式（0=经典 1=纤细）</param>
    public Task<EntityUserGameTexture> UploadLocalSkinAsync(byte[] pngBytes, string fileName, int skinMode = 0)
        => Task.Run(() => NPFLauncher.UploadSkinAsync(
            pngBytes,
            fileName,
            EnumGameClientType.Java,
            EnumTextureType.SKIN,
            skinMode));
}
