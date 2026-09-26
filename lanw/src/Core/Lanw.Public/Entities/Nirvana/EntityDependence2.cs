using System.Text.Json.Serialization;

namespace Lanw.Public.Entities.Nirvana;

/// <summary>服务器依赖项实体，由 Nirvana.Public.Entities.Nirvana.EntityDependence2 移植。</summary>
public class EntityDependence2 {
    [JsonPropertyName("id")]
    public required string Id { get; set; }
}
