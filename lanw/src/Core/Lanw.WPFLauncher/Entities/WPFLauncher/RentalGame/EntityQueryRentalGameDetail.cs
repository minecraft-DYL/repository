using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.WPFLauncher.RentalGame;

/// <summary>
/// 租赁服详细信息查询请求（由 Nirvana.WPFLauncher.Entities.WPFLauncher.RentalGame.EntityQueryRentalGameDetail 移植，自研）。
/// </summary>
public class EntityQueryRentalGameDetail
{
    [JsonPropertyName("server_id")]
    public string ServerId { get; set; } = string.Empty;
}
