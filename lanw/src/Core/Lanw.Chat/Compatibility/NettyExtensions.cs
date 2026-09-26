using System.Text;
using DotNetty.Buffers;

namespace Lanw.Chat.Extensions;

/// <summary>
///     IByteBuffer 的 Minecraft 协议读写扩展（由 Nirvana.DevPlugin.Extensions.NettyExtensions 与
///     Codexus.Development.SDK.Extensions 中被聊天室使用的成员移植，行为保持一致）。
/// </summary>
public static class NettyExtensions {
    /// <summary>读 VarInt（最多 5 字节）。</summary>
    public static int ReadVarIntFromBuffer(this IByteBuffer buffer)
    {
        var num = 0;
        var num2 = 0;
        while (true) {
            var b = buffer.ReadByte();
            num |= (b & 0x7F) << num2;
            if ((b & 0x80) == 0) {
                break;
            }

            num2 += 7;
            if (num2 >= 32) {
                throw new Exception("VarInt is too big");
            }
        }

        return num;
    }

    /// <summary>读 VarInt 长度前缀的 UTF-8 字符串。</summary>
    public static string ReadStringFromBuffer(this IByteBuffer buffer, int maxLength = short.MaxValue)
    {
        var num = buffer.ReadVarIntFromBuffer();
        if (num > maxLength * 4) {
            throw new Exception("The received encoded string buffer length is longer than maximum allowed (" + num + " > " + maxLength * 4 + ")");
        }

        if (num < 0) {
            throw new Exception("The received encoded string buffer length is less than zero! Weird string!");
        }

        if (num > buffer.ReadableBytes) {
            num = buffer.ReadableBytes;
        }

        var array = new byte[num];
        buffer.ReadBytes(array);
        var text = Encoding.UTF8.GetString(array);
        return text.Length > maxLength ? throw new Exception("The received string length is longer than maximum allowed (" + num + " > " + maxLength + ")") : text;
    }

    /// <summary>读尽剩余可读字节。</summary>
    public static byte[] ReadByteArrayReadableBytes(this IByteBuffer buffer)
    {
        var array = new byte[buffer.ReadableBytes];
        buffer.ReadBytes(array);
        return array;
    }

    /// <summary>写 VarInt。</summary>
    public static IByteBuffer WriteVarInt(this IByteBuffer buffer, int input)
    {
        while ((input & -128) != 0) {
            buffer.WriteByte((input & 0x7F) | 0x80);
            input >>>= 7;
        }

        buffer.WriteByte(input);
        return buffer;
    }

    /// <summary>写 VarInt 长度前缀的 UTF-8 字符串。</summary>
    public static IByteBuffer WriteStringToBuffer(this IByteBuffer buffer, string stringToWrite, int maxLength = short.MaxValue)
    {
        if (stringToWrite.Length > maxLength) {
            throw new Exception("String too big (was " + stringToWrite.Length + " bytes encoded, max " + maxLength + ")");
        }

        var bytes = Encoding.UTF8.GetBytes(stringToWrite);
        return buffer.WriteByteArrayToBuffer(bytes);
    }

    /// <summary>写 VarInt 长度前缀的字节数组。</summary>
    public static IByteBuffer WriteByteArrayToBuffer(this IByteBuffer buffer, byte[] bytes)
    {
        buffer.WriteVarInt(bytes.Length);
        buffer.WriteBytes(bytes);
        return buffer;
    }
}
