using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.Pc4399;

/// <summary>
/// 4399 统一认证数据（由 Nirvana.WPFLauncher.Entities.Pc4399.EntityC4399UniAuthData 移植，自研）。
/// </summary>
public class EntityC4399UniAuthData
{
    [JsonPropertyName("sdk_login_data")]
    public string SdkLoginData { get; set; } = string.Empty;
}