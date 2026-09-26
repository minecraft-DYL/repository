using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.WPFLauncher.RentalGame;

/// <summary>
/// 租赁服列表查询请求（由 Nirvana.WPFLauncher.Entities.WPFLauncher.RentalGame.EntityQueryRentalGame 移植，自研）。
/// </summary>
public class EntityQueryRentalGame
{
    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("sort_type")]
    public int SortType { get; set; }
}
