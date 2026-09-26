using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameDetails;

/// <summary>
/// 网络服详情视频信息（由 Nirvana.WPFLauncher.Entities.WPFLauncher.NetGame.GameDetails.EntityDetailsVideo 移植，自研）。
/// </summary>
public class EntityDetailsVideo
{
    [JsonPropertyName("cover")]
    public string Cover { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public int Size { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
