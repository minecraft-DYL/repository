using System.Text.Json.Serialization;

namespace Lanw.Game.Launcher.Entities.WPFLauncher.NetGame.GameLaunch.GameMods;

public class EntitySearchByIdsQuery {
    [JsonPropertyName("item_id_list")]
    public required List<ulong> ItemIdList { get; set; }
}
