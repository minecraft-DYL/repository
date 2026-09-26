using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.Pc4399;

/// <summary>
/// 4399 统一认证响应（由 Nirvana.WPFLauncher.Entities.Pc4399.EntityC4399UniAuth 移植，自研）。
/// </summary>
public class EntityC4399UniAuth
{
    [JsonPropertyName("data")]
    public EntityC4399UniAuthData Data { get; init; } = new();
}