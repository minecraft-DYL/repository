using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.Pc4399.Com4399;

/// <summary>
/// 4399com 登录用户信息响应（由 Nirvana.WPFLauncher.Entities.Pc4399.Com4399.Entity4399UserInfoResponse 移植，自研）。
/// </summary>
public class Entity4399UserInfoResponse
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public Entity4399UserInfoResult? Result { get; set; } = new();
}