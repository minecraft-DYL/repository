using Lanw.DevPlugin;
using Lanw.DevPlugin.Enums;
using Lanw.DevPlugin.Packet;
using Serilog;

namespace Lanw.Development.Packet.Configuration.Client;

public class CAcknowledgeConfiguration : DPacket {
    public static readonly RegisterPacket RegisterPacket = new(EnumConnectionState.Play, EnumPacketDirection.ServerBound, 12, EnumProtocolVersion.V1206, EnumProtocolVersion.V1210);
}
