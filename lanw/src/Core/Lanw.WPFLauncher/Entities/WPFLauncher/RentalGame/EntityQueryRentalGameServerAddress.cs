using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.WPFLauncher.RentalGame;

/// <summary>
/// 租赁服地址查询请求（由 Nirvana.WPFLauncher.Entities.WPFLauncher.RentalGame.EntityQueryRentalGameServerAddress 移植，自研）。
/// </summary>
public class EntityQueryRentalGameServerAddress
{
    [JsonPropertyName("server_id")]
    public string ServerId { get; set; } = string.Empty;

    [JsonPropertyName("pwd")]
    public string Password { get; set; } = string.Empty;
}
