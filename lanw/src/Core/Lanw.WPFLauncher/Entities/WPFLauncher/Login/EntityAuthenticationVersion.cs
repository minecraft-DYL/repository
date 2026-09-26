using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.WPFLauncher.Login;

/// <summary>
/// X19 认证版本（由 Nirvana.WPFLauncher.Entities.WPFLauncher.Login.EntityAuthenticationVersion 移植，自研）。
/// </summary>
public class EntityAuthenticationVersion
{
    [JsonPropertyName("version")]
    public required string Version { get; set; }

    [JsonPropertyName("launcher_md5")]
    public string LauncherMd5 { get; set; } = string.Empty;

    [JsonPropertyName("updater_md5")]
    public string UpdaterMd5 { get; set; } = string.Empty;
}