using System.Text.Json.Serialization;

namespace Lanw.Public.Entities.Plugin;

/// <summary>插件依赖实体（插件商城），由 Nirvana.Public.Entities.Plugin.EntityPluginDependency 移植。</summary>
public class EntityPluginDependency {
    [JsonPropertyName("id")]
    [JsonInclude]
    public required string Id { get; set; }

    [JsonPropertyName("name")]
    [JsonInclude]
    public required string Name { get; set; }
}
