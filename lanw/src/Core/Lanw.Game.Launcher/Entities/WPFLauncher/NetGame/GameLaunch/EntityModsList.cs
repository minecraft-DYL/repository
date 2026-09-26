using System.Text.Json.Serialization;
using Lanw.Game.Launcher.Entities.WPFLauncher.NetGame.GameLaunch.GameMods;

namespace Lanw.Game.Launcher.Entities.WPFLauncher.NetGame.GameLaunch;

public class EntityModsList {
    [JsonPropertyName("mods")]
    public List<EntityModsInfo> Mods { get; set; } = [];
}
