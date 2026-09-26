using System.Text.Json.Serialization;
using Lanw.WPFLauncher.Entities.WPFLauncher.Minecraft;

namespace Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameLaunch.Texture;

/// <summary>
/// 用户游戏贴图（由 Nirvana.WPFLauncher.Entities.WPFLauncher.NetGame.GameLaunch.Texture.EntityUserGameTexture 移植，自研）。
/// 对应端点 /user-game-skin/query/search-by-type 的响应元素。
/// </summary>
public class EntityUserGameTexture
{
    [JsonPropertyName("entity_id")]
    public string EntityId { get; set; } = string.Empty;

    [JsonPropertyName("game_type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EnumGType GameType { get; set; }

    [JsonPropertyName("skin_type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EnumTextureType SkinType { get; set; }

    [JsonPropertyName("skin_id")]
    public string SkinId { get; set; } = string.Empty;

    [JsonPropertyName("skin_mode")]
    public int SkinMode { get; set; }

    [JsonPropertyName("client_type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EnumGameClientType ClientType { get; set; }
}
