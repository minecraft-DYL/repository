using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.MPay;

/// <summary>
/// MPay 客户端扩展信息（由 Nirvana.WPFLauncher.Entities.MPay.EntityPcExtInfo 移植，自研）。
/// </summary>
public class EntityPcExtInfo
{
    [JsonPropertyName("src_jf_game_id")]
    public string SrcJfGameId { get; set; } = string.Empty;

    [JsonPropertyName("src_app_channel")]
    public string SrcAppChannel { get; set; } = string.Empty;

    [JsonPropertyName("from_game_id")]
    public string FromGameId { get; set; } = string.Empty;

    [JsonPropertyName("src_client_type")]
    public int SrcClientType { get; set; }

    [JsonPropertyName("src_udid")]
    public string SrcUdid { get; set; } = string.Empty;

    [JsonPropertyName("src_sdk_version")]
    public string SrcSdkVersion { get; set; } = string.Empty;

    [JsonPropertyName("src_pay_channel")]
    public string SrcPayChannel { get; set; } = string.Empty;

    [JsonPropertyName("src_client_ip")]
    public string SrcClientIp { get; set; } = string.Empty;

    [JsonPropertyName("extra_unisdk_data")]
    public string ExtraUnisdkData { get; set; } = string.Empty;
}