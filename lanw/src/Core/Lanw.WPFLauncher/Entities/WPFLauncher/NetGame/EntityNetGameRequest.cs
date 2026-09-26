using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.WPFLauncher.NetGame;

/// <summary>
/// 网络服列表查询请求（由 Nirvana.WPFLauncher.Entities.WPFLauncher.NetGame.EntityNetGameRequest 移植，自研）。
/// </summary>
public class EntityNetGameRequest
{
    [JsonPropertyName("available_mc_versions")]
    public required string[] AvailableMcVersions { get; set; }

    [JsonPropertyName("item_type")]
    public required int ItemType { get; set; }

    [JsonPropertyName("length")]
    public required int Length { get; set; }

    [JsonPropertyName("offset")]
    public required int Offset { get; set; }

    [JsonPropertyName("master_type_id")]
    public required string MasterTypeId { get; set; }

    [JsonPropertyName("secondary_type_id")]
    public required string SecondaryTypeId { get; set; }
}
