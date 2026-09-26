using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Lanw.Cipher.Entities.Yggdrasil;

/// <summary>
/// mod 列表（由 Nirvana.Cipher.Entities.Yggdrasil.ModList 移植，自研）。
/// </summary>
public class ModList
{
    [JsonPropertyName("mods")]
    public List<Mod> Mods { get; init; } = [];
}