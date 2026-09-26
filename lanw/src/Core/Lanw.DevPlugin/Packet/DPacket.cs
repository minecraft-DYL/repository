using DotNetty.Buffers;

namespace Lanw.DevPlugin.Packet;

public abstract class DPacket : BPacket {
    public override void ReadFromBuffer(BGameConnection connection, IByteBuffer buffer) { }

    public override void WriteToBuffer(IByteBuffer buffer) { }
}
