using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lanw.Cipher.Entities.Yggdrasil;

/// <summary>
/// 网易 Yggdrasil 登录用的游戏档案（由 Nirvana.Cipher.Entities.Yggdrasil.GameProfile 移植，自研）。
/// </summary>
public class GameProfile
{
    [JsonPropertyName("gameId")]
    public required string GameId { get; init; }

    [JsonPropertyName("gameVersion")]
    public required string GameVersion { get; init; }

    [JsonPropertyName("bootstrapMd5")]
    public required string BootstrapMd5 { get; init; }

    [JsonPropertyName("datFileMd5")]
    public required string DatFileMd5 { get; init; }

    [JsonPropertyName("mods")]
    public required ModList? Mods { get; init; }

    [JsonPropertyName("profile")]
    public required UserProfile User { get; init; }

    public string GetModInfo()
    {
        return Mods == null ? string.Empty : JsonSerializer.Serialize(Mods);
    }
}