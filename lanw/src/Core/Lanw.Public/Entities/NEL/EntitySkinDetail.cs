using System.Text.Json.Serialization;
using Lanw.Core.Utils.CodeTools;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameSkin;
using Lanw.WPFLauncher.Protocol;

namespace Lanw.Public.Entities.NEL;

/// <summary>
/// 皮肤详情（由 Nirvana.Public.Entities.NEL.EntitySkinDetail 移植，自研）。
/// 构造时按皮肤ID拉取详情并展开为可序列化字段（发布时间 unix 时间戳转为文本）。
/// 注：原参考构造后调用 CacheManager.ClearCacheImage(item) 清理皮肤缓存图片，
/// 缓存引擎属网络服/皮肤缓存模块（t14 移植中），此处省略该副作用调用。
/// </summary>
public class EntitySkinDetail
{
    public EntitySkinDetail(string id)
    {
        Set(NPFLauncher.GetSkinDetailsAsync(id).GetAwaiter().GetResult());
    }

    [JsonPropertyName("entity_id")]
    [JsonInclude]
    public string? EntityId { get; set; }

    [JsonPropertyName("brief_summary")]
    [JsonInclude]
    public string? BriefSummary { get; set; }

    [JsonPropertyName("name")]
    [JsonInclude]
    public string? Name { get; set; }

    [JsonPropertyName("title_image_url")]
    [JsonInclude]
    public string? TitleImageUrl { get; set; }

    [JsonPropertyName("like_num")]
    [JsonInclude]
    public int? LikeNum { get; set; }

    [JsonPropertyName("developer_name")]
    [JsonInclude]
    public string? DeveloperName { get; set; }

    [JsonPropertyName("publish_time")]
    [JsonInclude]
    public string? PublishTime { get; set; }

    [JsonPropertyName("download_num")]
    [JsonInclude]
    public long? DownloadNum { get; set; }

    private void Set(EntityQueryNetSkinItem? item)
    {
        if (item == null)
        {
            throw new ErrorCodeException(ErrorCode.IdError);
        }

        // CacheManager.ClearCacheImage(item)：皮肤缓存图片清理（t14 移植中，暂省略）
        DeveloperName = item.DeveloperName;
        // unix 时间戳 转换为 文本
        PublishTime = DateTimeOffset.FromUnixTimeSeconds(item.PublishTime).ToString("yyyy-MM-dd");
        DownloadNum = item.DownloadNum;
        EntityId = item.EntityId;
        BriefSummary = item.BriefSummary;
        Name = item.Name;
        TitleImageUrl = item.TitleImageUrl;
        LikeNum = item.LikeNum;
    }
}
