using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using Lanw.DevPlugin.Extensions;

namespace Lanw.Development.Analysis;

public class MessageSerializer21Bit : MessageToByteEncoder<IByteBuffer> {
    protected override void Encode(IChannelHandlerContext context, IByteBuffer message, IByteBuffer output)
    {
        var readableBytes = message.ReadableBytes;
        var varIntSize = readableBytes.GetVarIntSize();
        if (varIntSize <= 3) {
            output.EnsureWritable(varIntSize + readableBytes);
            output.WriteVarInt(readableBytes);
            output.WriteBytes(message, message.ReaderIndex, readableBytes);
        }
    }
}
