using DotNetty.Buffers;
using Lanw.Development.Connection;
using Lanw.DevPlugin;
using Lanw.DevPlugin.Enums;
using Lanw.DevPlugin.Extensions;
using Lanw.DevPlugin.Packet;

namespace Lanw.Development.Packet.Login.Server;

public class SPacketEnableCompression : DPacket {
    public static readonly RegisterPacket RegisterPacket = new(EnumConnectionState.Login, EnumPacketDirection.ClientBound, 3);

    private int CompressionThreshold { get; set; }

    public override void ReadFromBuffer(BGameConnection connection, IByteBuffer buffer)
    {
        CompressionThreshold = buffer.ReadVarIntFromBuffer();
    }

    public override bool HandlePacket(BGameConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection.ServerChannel);
        GameConnection.EnableCompression(connection.ServerChannel, CompressionThreshold);
        return true;
    }
}
