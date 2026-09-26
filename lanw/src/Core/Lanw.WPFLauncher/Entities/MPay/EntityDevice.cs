using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.MPay;

/// <summary>
/// MPay 设备（id / 加密 key）（由 Nirvana.WPFLauncher.Entities.MPay.EntityDevice 移植，自研）。
/// </summary>
public class EntityDevice
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;
}