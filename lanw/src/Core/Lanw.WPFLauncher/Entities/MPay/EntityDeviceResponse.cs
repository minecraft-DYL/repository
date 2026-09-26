using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.MPay;

/// <summary>
/// MPay 创建设备响应（由 Nirvana.WPFLauncher.Entities.MPay.EntityDeviceResponse 移植，自研）。
/// </summary>
public class EntityDeviceResponse
{
    [JsonPropertyName("device")]
    public EntityDevice EntityDevice { get; set; } = new();
}