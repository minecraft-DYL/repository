using System.Text.Json.Serialization;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame;

namespace Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameDetails;

/// <summary>
/// 网络服详情查询请求（由 Nirvana.WPFLauncher.Entities.WPFLauncher.NetGame.GameDetails.EntityQueryNetGameDetailRequest 移植，自研）。
/// </summary>
public class EntityQueryNetGameDetailRequest
{
    [JsonPropertyName("item_id")]
    public required string ItemId { get; init; }
}
