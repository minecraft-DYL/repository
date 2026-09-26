using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.MPay;

/// <summary>
/// MPay 登录用户响应（由 Nirvana.WPFLauncher.Entities.MPay.EntityMPayUserResponse 移植，自研）。
/// </summary>
public class EntityMPayUserResponse
{
    [JsonPropertyName("force_pwd")]
    public bool ForcePwd { get; set; }

    [JsonPropertyName("verify_status")]
    public EntityVerifyStatus? VerifyStatus { get; set; }

    [JsonPropertyName("user")]
    public required EntityMPayUser User { get; set; } = new();
}