using Lanw.DevPlugin;
using Lanw.DevPlugin.Enums;
using Lanw.DevPlugin.Packet;
using Serilog;

namespace Lanw.Development.Packet.Configuration.Server;

public class SStartConfiguration : DPacket {
    public static readonly RegisterPacket RegisterPacket = new(EnumConnectionState.Play, EnumPacketDirection.ClientBound, 105, EnumProtocolVersion.V1206, EnumProtocolVersion.V1210);

    public override bool HandlePacket(BGameConnection connection)
    {
        connection.State = EnumConnectionState.Configuration;
        Log.Information("Starting Configuration.");
        return false;
    }
}
