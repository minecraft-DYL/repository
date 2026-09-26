using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.WPFLauncher.RentalGame.GameCharacters;

/// <summary>
/// 租赁服玩家列表查询请求（由 Nirvana.WPFLauncher.Entities.WPFLauncher.RentalGame.GameCharacters.EntityQueryRentalGamePlayerList 移植，自研）。
/// </summary>
public class EntityQueryRentalGamePlayerList
{
    [JsonPropertyName("server_id")]
    public string ServerId { get; set; } = string.Empty;

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("length")]
    public int Length { get; set; }
}
