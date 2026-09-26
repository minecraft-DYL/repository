using Lanw.Development.Manager;
using Lanw.Heypixel.Configuration;
using Lanw.Heypixel.Play;
using Serilog;

namespace Lanw.Heypixel;

/// <summary>
/// 花雨庭（Heypixel）协议适配（由 Nirvana.Heypixel.HeypixelProtocol 移植）。
/// 注册内置封包，供代理模式（InterceptorManager）使用。
/// </summary>
public class HeypixelProtocol {
    public const string GameId = "4661334467366178884";

    static HeypixelProtocol()
    {
        PacketManager.BasePackets.Add(new C2SConfigPluginMessage(), C2SConfigPluginMessage.RegisterPacket);
        PacketManager.BasePackets.Add(new SaClientboundSetPlayerTeamPacket(), SaClientboundSetPlayerTeamPacket.RegisterPacket);
    }

    public static void Init()
    {
        Log.Information("[Heypixel] Initializing.");
    }
}
