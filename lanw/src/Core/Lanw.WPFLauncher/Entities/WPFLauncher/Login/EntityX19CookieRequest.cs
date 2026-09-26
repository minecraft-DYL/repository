using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.WPFLauncher.Login;

/// <summary>
/// X19 Cookie 请求（sauth_json 包裹）（由 Nirvana.WPFLauncher.Entities.WPFLauncher.Login.EntityX19CookieRequest 移植，自研）。
/// </summary>
public class EntityX19CookieRequest
{
    [JsonPropertyName("sauth_json")]
    public required string Json { get; init; }
}