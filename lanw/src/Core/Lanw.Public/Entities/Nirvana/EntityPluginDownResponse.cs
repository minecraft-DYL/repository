using System.Text.Json.Serialization;

namespace Lanw.Public.Entities.Nirvana;

/// <summary>插件下载信息实体（含依赖），由 Nirvana.Public.Entities.Nirvana.EntityPluginDownResponse 移植。</summary>
public class EntityPluginDownResponse {
    [JsonPropertyName("fileHash")]
    [JsonInclude]
    public string? FileHash { get; set; }

    [JsonPropertyName("fileSize")]
    [JsonInclude]
    public long? FileSize { get; set; }

    [JsonPropertyName("id")]
    [JsonInclude]
    public required string Id { get; set; }

    [JsonPropertyName("dependencies")]
    [JsonInclude]
    public EntityPluginDownResponse[]? Dependencies { get; set; }
}
