using Lanw.Chat.Manager;
using Lanw.Chat.Packet.IPacket;
using Lanw.Chat.Packet.V108X.Chat;
using Lanw.Chat.Packet.V1122.Chat;
using Lanw.Chat.Packet.V1180.Chat;
using Lanw.Chat.Packet.V1200.Chat;

namespace Lanw.Chat;

/// <summary>
///     聊天室插件入口，由 Nirvana.Chat.ChatPluginMain 移植。
/// </summary>
public static class ChatPluginMain {
    private static readonly List<APacket> Packets = [
        new C01PacketChatMessage(),
        // 1122
        new CPacketChatMessage(),
        // 1180
        new ServerboundChatPacket(),
        // 1200
        new ServerboundChatCommandPacket(),
        // 1206
        new Packet.V1206.Chat.ServerboundChatCommandPacket()
    ];

    public static void Initialize()
    {
        NPacketManager.RegisterPacketFromList("Lanw.Chat", Packets, ChatManager.Register);
    }
}
