using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameSkin;

/// <summary>
/// 皮肤详情批量查询请求（由 Nirvana.WPFLauncher.Entities.WPFLauncher.NetGame.GameSkin.EntitySkinDetailsRequest 移植，自研）。
/// 对应端点 /item/query/search-by-ids。
/// </summary>
public class EntitySkinDetailsRequest
{
    [JsonPropertyName("channel_id")]
    public required int ChannelId { get; set; }

    [JsonPropertyName("entity_ids")]
    public required List<string> EntityIds { get; set; }

    [JsonPropertyName("is_has")]
    public required bool IsHas { get; set; }

    [JsonPropertyName("with_price")]
    public required bool WithPrice { get; set; }

    [JsonPropertyName("with_title_image")]
    public required bool WithTitleImage { get; set; }
}
