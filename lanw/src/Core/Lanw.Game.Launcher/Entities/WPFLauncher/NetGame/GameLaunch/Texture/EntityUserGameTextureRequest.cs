using System.Text.Json.Serialization;
using Lanw.Game.Launcher.Entities.WPFLauncher.Minecraft;

namespace Lanw.Game.Launcher.Entities.WPFLauncher.NetGame.GameLaunch.Texture;

public class EntityUserGameTextureRequest {
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("game_type")]
    public string GameType { get; set; } = string.Empty;

    [JsonPropertyName("client_type")]
    public EnumGameClientType ClientType { get; set; }
}
