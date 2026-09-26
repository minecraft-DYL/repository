using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.WPFLauncher.NetGame;

/// <summary>
/// 网络服连接地址（由 Nirvana.WPFLauncher.Entities.WPFLauncher.NetGame.EntityNetGameServerAddress 移植，自研）。
/// </summary>
public class EntityNetGameServerAddress
{
    [JsonPropertyName("ip")]
    public required string Host { get; init; }

    [JsonPropertyName("port")]
    public required int Port { get; init; }
}
