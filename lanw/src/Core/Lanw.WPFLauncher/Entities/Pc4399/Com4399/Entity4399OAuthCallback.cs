using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.Pc4399.Com4399;

/// <summary>
/// 4399 OAuth 回调（由 Nirvana.WPFLauncher.Entities.Pc4399.Com4399.Entity4399OAuthCallback 移植，自研）。
/// </summary>
public class Entity4399OAuthCallback
{
    [JsonPropertyName("result")]
    public required string Result { get; set; }
}