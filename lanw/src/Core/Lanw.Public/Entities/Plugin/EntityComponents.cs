using System.Text.Json.Serialization;

namespace Lanw.Public.Entities.Plugin;

/// <summary>插件商城列表项实体，由 Nirvana.Public.Entities.Plugin.EntityComponents 移植。</summary>
public class EntityComponents {
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("shortDescription")]
    public string? ShortDescription { get; set; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; set; }

    [JsonPropertyName("downloadCount")]
    public long? DownloadCount { get; set; }
}
