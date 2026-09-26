using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.MPay;

/// <summary>
/// MPay 登录用户请求参数（由 Nirvana.WPFLauncher.Entities.MPay.EntityUsersParameters 移植，自研）。
/// </summary>
public class EntityUsersParameters
{
    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("unique_id")]
    public string Unique { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;
}