using System.Text.Json.Serialization;

namespace Lanw.Cipher.Entities.Yggdrasil;

/// <summary>
/// 网易认证服务器条目（由 Nirvana.Cipher.Entities.Yggdrasil.YggdrasilServer 移植，自研）。
/// </summary>
public class YggdrasilServer
{
    [JsonPropertyName("IP")]
    public required string Ip { get; set; }

    [JsonPropertyName("Port")]
    public required int Port { get; set; }

    [JsonPropertyName("ServerType")]
    public required string ServerType { get; set; }
}