using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.WPFLauncher.NetGame;

/// <summary>
/// 网络服支持的 MC 版本（由 Nirvana.WPFLauncher.Entities.WPFLauncher.NetGame.EntityMcVersion 移植，自研）。
/// </summary>
public class EntityMcVersion
{
    [JsonPropertyName("mcversionid")]
    public int McVersionId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
