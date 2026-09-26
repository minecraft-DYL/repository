using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.MPay;

/// <summary>
/// MPay 校验响应（错误场景）（由 Nirvana.WPFLauncher.Entities.MPay.EntityVerifyResponse 移植，自研）。
/// </summary>
public class EntityVerifyResponse
{
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("verify_url")]
    public string VerifyUrl { get; set; } = string.Empty;
}