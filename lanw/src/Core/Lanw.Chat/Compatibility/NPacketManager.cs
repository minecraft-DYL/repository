using Lanw.Chat.Packet.IPacket;

namespace Lanw.Chat.Manager;

/// <summary>
///     数据包注册中心（对应参考源 NirvanaAPI 的 NPacketManager 中聊天室使用的入口）。
///
///     参考实现把插件包注册进全局 PacketManager 并触发插件注册回调；
///     此处保留相同调用形态（模块名 + 包列表 + 注册回调），并保留已注册列表供代理层接入与测试。
/// </summary>
public static class NPacketManager {
    private static readonly List<(string PluginName, APacket Packet)> Packets = [];

    /// <summary>已注册的数据包（模块名，包实例）。</summary>
    public static IReadOnlyList<(string PluginName, APacket Packet)> RegisteredPackets => Packets;

    /// <summary>按插件模块名注册一批数据包，并执行插件自身的注册回调。</summary>
    public static void RegisterPacketFromList(string pluginName, IEnumerable<APacket> packets, Action register)
    {
        foreach (var packet in packets) {
            if (!Packets.Any(item => item.PluginName == pluginName && item.Packet.GetType() == packet.GetType())) {
                Packets.Add((pluginName, packet));
            }
        }

        register();
    }
}
