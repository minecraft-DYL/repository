namespace Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameLaunch.Texture;

/// <summary>
/// 游戏类型（由 Nirvana.WPFLauncher.Entities.WPFLauncher.NetGame.GameLaunch.Texture.EnumGType 移植，自研）。
/// 对应用户游戏贴图的 game_type 字段。
/// </summary>
public enum EnumGType
{
    None = -1, // 0xFFFFFFFF
    SingleGame = 1, // 单人游戏
    NetGame = 2, // 网络游戏
    McGame = 7,
    ServerGame = 8, // 租赁服游戏
    LanGame = 9, // 本地联机
    OnlineLobbyGame = 10 // 0x0000000A
}
