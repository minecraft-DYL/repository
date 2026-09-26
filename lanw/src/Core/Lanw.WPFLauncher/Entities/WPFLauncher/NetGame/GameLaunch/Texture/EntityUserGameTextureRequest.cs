using System.Text.Json.Serialization;
using Lanw.WPFLauncher.Entities.WPFLauncher.Minecraft;

namespace Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameLaunch.Texture;

/// <summary>
/// 用户游戏贴图查询请求（由 Nirvana.WPFLauncher.Entities.WPFLauncher.NetGame.GameLaunch.Texture.EntityUserGameTextureRequest 移植，自研）。
/// 对应端点 /user-game-skin/query/search-by-type。
/// </summary>
public class EntityUserGameTextureRequest
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("game_type")]
    public string GameType { get; set; } = string.Empty;

    [JsonPropertyName("client_type")]
    public EnumGameClientType ClientType { get; set; }
}
