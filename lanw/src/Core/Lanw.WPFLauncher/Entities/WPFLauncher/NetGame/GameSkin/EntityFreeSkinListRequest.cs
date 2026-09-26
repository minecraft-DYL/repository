using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameSkin;

/// <summary>
/// 免费皮肤列表查询请求（由 Nirvana.WPFLauncher.Entities.WPFLauncher.NetGame.GameSkin.EntityFreeSkinListRequest 移植，自研）。
/// 供账号自动登录校验使用。
/// </summary>
public class EntityFreeSkinListRequest
{
    [JsonPropertyName("is_has")]
    public required bool IsHas { get; set; }

    [JsonPropertyName("item_type")]
    public required int ItemType { get; set; }

    [JsonPropertyName("length")]
    public required int Length { get; set; }

    [JsonPropertyName("master_type_id")]
    public required int MasterTypeId { get; set; }

    [JsonPropertyName("offset")]
    public required int Offset { get; set; }

    [JsonPropertyName("price_type")]
    public required int PriceType { get; set; }

    [JsonPropertyName("secondary_type_id")]
    public required int SecondaryTypeId { get; set; }
}