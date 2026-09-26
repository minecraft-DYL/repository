using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameSkin;

/// <summary>
/// 皮肤应用设置项（由 Nirvana.WPFLauncher.Entities.WPFLauncher.NetGame.GameSkin.EntitySkinSettings 移植，自研）。
/// 对应端点 /user-game-skin-multi 的 skin_settings 元素。
/// </summary>
public class EntitySkinSettings
{
    [JsonPropertyName("client_type")]
    public required string ClientType { get; set; }

    [JsonPropertyName("game_type")]
    public required int GameType { get; set; }

    [JsonPropertyName("skin_id")]
    public required string SkinId { get; set; }

    [JsonPropertyName("skin_mode")]
    public required int SkinMode { get; set; }

    [JsonPropertyName("skin_type")]
    public required int SkinType { get; set; }
}
