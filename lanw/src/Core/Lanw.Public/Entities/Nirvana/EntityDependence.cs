using System.Text.Json.Serialization;

namespace Lanw.Public.Entities.Nirvana;

/// <summary>服务器依赖实体，由 Nirvana.Public.Entities.Nirvana.EntityDependence 移植。</summary>
public class EntityDependence {
    [JsonPropertyName("data")]
    public required EntityDependence2[] Data { get; set; }
}
