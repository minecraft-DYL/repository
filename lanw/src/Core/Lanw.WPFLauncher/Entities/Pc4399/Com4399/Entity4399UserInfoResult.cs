using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.Pc4399.Com4399;

/// <summary>
/// 4399com 登录用户信息结果（由 Nirvana.WPFLauncher.Entities.Pc4399.Com4399.Entity4399UserInfoResult 移植，自研）。
/// </summary>
public class Entity4399UserInfoResult
{
    [JsonPropertyName("uid")]
    public long Uid { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;
}