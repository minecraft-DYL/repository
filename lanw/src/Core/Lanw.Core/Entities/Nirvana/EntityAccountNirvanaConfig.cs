using System.Text.Json.Serialization;

namespace Lanw.Core.Entities.Nirvana;

public class EntityAccountNirvanaConfig {
    [JsonPropertyName("account")]
    [JsonInclude]
    public required string Account { get; set; }

    [JsonPropertyName("days")]
    [JsonInclude]
    public required double Days { get; set; }

    [JsonPropertyName("hideAccount")]
    [JsonInclude]
    public required bool HideAccount { get; init; }
}