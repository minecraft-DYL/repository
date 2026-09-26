using System.Text.Json.Serialization;

namespace Lanw.Public.Entities.Login;

/// <summary>
/// 4399 验证码校验结果（由 Nirvana.Public.Entities.Login.Entity4399CaptchaOk 移植，自研）。
/// </summary>
public class Entity4399CaptchaOk
{
    [JsonPropertyName("captcha")]
    [JsonInclude]
    public string? Captcha { get; set; }
}