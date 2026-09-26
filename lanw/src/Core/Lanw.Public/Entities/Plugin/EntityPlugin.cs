using System.Text.Json.Serialization;

namespace Lanw.Public.Entities.Plugin;

/// <summary>插件详情实体（插件商城），由 Nirvana.Public.Entities.Plugin.EntityPlugin 移植。</summary>
public class EntityPlugin {
    [JsonPropertyName("detailDescription")]
    [JsonInclude]
    public string? DetailDescription { get; set; }

    [JsonPropertyName("downloadCount")]
    [JsonInclude]
    public int? DownloadCount { get; set; }

    [JsonPropertyName("id")]
    [JsonInclude]
    public string? Id { get; set; }

    [JsonPropertyName("logoUrl")]
    [JsonInclude]
    public string? LogoUrl { get; set; }

    [JsonPropertyName("name")]
    [JsonInclude]
    public string? Name { get; set; }

    [JsonPropertyName("publishDate")]
    [JsonInclude]
    public string? PublishDate { get; set; }

    [JsonPropertyName("publisher")]
    [JsonInclude]
    public string? Publisher { get; set; }

    [JsonPropertyName("shortDescription")]
    [JsonInclude]
    public string? ShortDescription { get; set; }

    [JsonPropertyName("version")]
    [JsonInclude]
    public string? Version { get; set; }

    [JsonPropertyName("dependencies")]
    [JsonInclude]
    public EntityPluginDependency[]? Dependencies { get; set; }
}
