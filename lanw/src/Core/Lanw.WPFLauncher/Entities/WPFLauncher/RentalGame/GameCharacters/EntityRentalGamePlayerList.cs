using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.WPFLauncher.RentalGame.GameCharacters;

/// <summary>
/// 租赁服玩家角色信息（由 Nirvana.WPFLauncher.Entities.WPFLauncher.RentalGame.GameCharacters.EntityRentalGamePlayerList 移植，自研）。
/// </summary>
public class EntityRentalGamePlayerList
{
    [JsonPropertyName("entity_id")]
    public string EntityId { get; set; } = string.Empty;

    [JsonPropertyName("server_id")]
    public string ServerId { get; set; } = string.Empty;

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("create_ts")]
    public ulong CreateTs { get; set; }

    [JsonPropertyName("delete_ts")]
    public ulong DeleteTs { get; set; }

    [JsonPropertyName("is_online")]
    public bool IsOnline { get; set; }
}
