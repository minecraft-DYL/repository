using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.WPFLauncher.RentalGame.GameCharacters;

/// <summary>
/// 租赁服创建游戏角色请求（由 Nirvana.WPFLauncher.Entities.WPFLauncher.RentalGame.GameCharacters.EntityAddRentalGameRole 移植，自研）。
/// </summary>
public class EntityAddRentalGameRole
{
    [JsonPropertyName("server_id")]
    public string ServerId { get; set; } = string.Empty;

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("create_ts")]
    public int CreateTs { get; set; }

    [JsonPropertyName("is_online")]
    public bool IsOnline { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }
}
