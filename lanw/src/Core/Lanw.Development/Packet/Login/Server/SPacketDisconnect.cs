using DotNetty.Buffers;
using Lanw.DevPlugin;
using Lanw.DevPlugin.Enums;
using Lanw.DevPlugin.Extensions;
using Lanw.DevPlugin.Packet;
using Serilog;

namespace Lanw.Development.Packet.Login.Server;

public class SPacketDisconnect : FPacket {
    public static readonly RegisterPacket RegisterPacket = new(EnumConnectionState.Login, EnumPacketDirection.ClientBound, 0);

    private string? _reason;

    public override void ReadFromBuffer(BGameConnection connection, IByteBuffer buffer)
    {
        base.ReadFromBuffer(buffer);
        _reason = buffer.ReadStringFromBuffer();
    }

    public override bool HandlePacket(BGameConnection connection)
    {
        Log.Debug("Disconnect Reason: {0}", _reason);
        return false;
    }
}
