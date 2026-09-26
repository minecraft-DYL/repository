using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using Lanw.DevPlugin.Extensions;
using Lanw.DevPlugin.Packet;

namespace Lanw.Development.Analysis;

public class MessageSerializer : MessageToByteEncoder<IPacket> {
    protected override void Encode(IChannelHandlerContext context, IPacket message, IByteBuffer output)
    {
        output.WriteVarInt(message.PacketId);
        message.WriteToBuffer(output);
    }
}
