using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameSkin;

/// <summary>
/// 网络皮肤条目（由 Nirvana.WPFLauncher.Entities.WPFLauncher.NetGame.GameSkin.EntityQueryNetSkinItem 移植，自研）。
/// 供账号自动登录校验使用。
/// </summary>
public class EntityQueryNetSkinItem
{
    [JsonPropertyName("entity_id")]
    public required string EntityId { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("developer_name")]
    public required string DeveloperName { get; set; }

    [JsonPropertyName("brief_summary")]
    public required string BriefSummary { get; set; }

    [JsonPropertyName("publish_time")]
    public required long PublishTime { get; set; }

    [JsonPropertyName("download_num")]
    public required long DownloadNum { get; set; }

    [JsonPropertyName("like_num")]
    public required int LikeNum { get; set; }

    [JsonPropertyName("title_image_url")]
    public string TitleImageUrl { get; set; } = string.Empty;

    public bool TitleImageSafe()
    {
        return TitleImageUrl.Contains("http") || TitleImageUrl.Contains("/image/");
    }
}