using System.Text.Json.Serialization;

namespace Lanw.Cipher.Entities.Yggdrasil;

/// <summary>
/// mod 信息（由 Nirvana.Cipher.Entities.Yggdrasil.Mod 移植，自研）。
/// </summary>
public class Mod
{
    [JsonPropertyName("modPath")]
    public required string ModPath { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("iid")]
    public required string Iid { get; set; }

    [JsonPropertyName("md5")]
    public required string Md5 { get; set; }

    [JsonPropertyName("version")]
    public required string Version { get; set; } = string.Empty;
}