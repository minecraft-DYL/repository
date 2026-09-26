using DotNetty.Buffers;
using DotNetty.Transport.Channels.Embedded;
using Lanw.Development.Analysis;
using Lanw.Development.Connection;
using Lanw.Development.Manager;
using Lanw.Development.Packet.Handshake.Client;
using Lanw.DevPlugin.Entities;
using Lanw.DevPlugin.Enums;
using Lanw.DevPlugin.Extensions;

namespace Lanw.Development.Tests;

/// <summary>
/// Lanw.Development（代理模式拦截器）移植验证：
/// 21bit 长度前缀序列化往返、压缩/加密编解码往返、内置包注册表与握手改写行为。
/// </summary>
public class DevelopmentPortTests {
    private static byte[] Compressible(int length)
    {
        var array = new byte[length];
        for (var i = 0; i < length; i++) {
            array[i] = (byte)('A' + i % 16);
        }

        return array;
    }

    private static byte[] ToArray(IByteBuffer buffer)
    {
        var array = new byte[buffer.ReadableBytes];
        buffer.ReadBytes(array);
        return array;
    }

    private static InterceptorConfig NewConfig()
    {
        return new InterceptorConfig {
            IsRental = false,
            LocalPort = 25565,
            NickName = "ProxyUser",
            ForwardAddress = "127.0.0.1",
            ForwardPort = 25565,
            ServerName = "Nirvana Server",
            ServerVersion = "1.20",
            ModInfo = string.Empty,
            GameId = "test-game"
        };
    }

    [Fact]
    public void VarInt_WriteRead_Roundtrip_And_Size()
    {
        var buffer = Unpooled.Buffer(16);
        foreach (var value in new[] { 0, 1, 2, 127, 128, 255, 300, 2097151, 2097152, int.MaxValue }) {
            buffer.Clear();
            buffer.WriteVarInt(value);
            Assert.Equal(value.GetVarIntSize(), buffer.ReadableBytes);
            Assert.Equal(value, buffer.ReadVarIntFromBuffer());
        }
    }

    [Fact]
    public void MessageSerializer21Bit_To_Deserializer21Bit_Roundtrip()
    {
        var payload = Compressible(300);

        var encoder = new EmbeddedChannel(new MessageSerializer21Bit());
        Assert.True(encoder.WriteOutbound(Unpooled.WrappedBuffer(payload)));
        var frame = encoder.ReadOutbound<IByteBuffer>();
        Assert.NotNull(frame);
        // 长度前缀（1 字节）+ 负载
        Assert.Equal(payload.Length.GetVarIntSize() + payload.Length, frame!.ReadableBytes);

        var decoder = new EmbeddedChannel(new MessageDeserializer21Bit());
        Assert.True(decoder.WriteInbound(frame));
        var decoded = decoder.ReadInbound<IByteBuffer>();
        Assert.NotNull(decoded);
        Assert.Equal(payload, ToArray(decoded!));
        Assert.Null(decoder.ReadInbound<IByteBuffer>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(1024)]
    public void MessageSerializer21Bit_Empty_And_Various_Lenths(int length)
    {
        var payload = Compressible(length);

        var encoder = new EmbeddedChannel(new MessageSerializer21Bit());
        Assert.True(encoder.WriteOutbound(Unpooled.WrappedBuffer(payload)));

        var decoder = new EmbeddedChannel(new MessageDeserializer21Bit());
        Assert.True(decoder.WriteInbound(encoder.ReadOutbound<IByteBuffer>()));
        var decoded = decoder.ReadInbound<IByteBuffer>();
        Assert.Equal(payload, ToArray(decoded!));
    }

    [Fact]
    public void MessageDeserializer21Bit_Handles_Fragmented_And_Concatenated_Frames()
    {
        var first = Compressible(200);
        var second = Compressible(64);

        var encoder = new EmbeddedChannel(new MessageSerializer21Bit());
        Assert.True(encoder.WriteOutbound(Unpooled.WrappedBuffer(first)));
        Assert.True(encoder.WriteOutbound(Unpooled.WrappedBuffer(second)));
        var frame1 = ToArray(encoder.ReadOutbound<IByteBuffer>()!);
        var frame2 = ToArray(encoder.ReadOutbound<IByteBuffer>()!);

        // 粘包：一帧内两个包
        var decoder = new EmbeddedChannel(new MessageDeserializer21Bit());
        var both = new byte[frame1.Length + frame2.Length];
        Array.Copy(frame1, 0, both, 0, frame1.Length);
        Array.Copy(frame2, 0, both, frame1.Length, frame2.Length);
        Assert.True(decoder.WriteInbound(Unpooled.WrappedBuffer(both)));
        Assert.Equal(first, ToArray(decoder.ReadInbound<IByteBuffer>()!));
        Assert.Equal(second, ToArray(decoder.ReadInbound<IByteBuffer>()!));
        Assert.Null(decoder.ReadInbound<IByteBuffer>());

        // 拆包：长度前缀与负载分两次到达
        var fragmented = new EmbeddedChannel(new MessageDeserializer21Bit());
        // 半帧被解码器累积（不产生输出，故 WriteInbound 返回 false 属正常）
        fragmented.WriteInbound(Unpooled.WrappedBuffer(frame1[..1]));
        Assert.Null(fragmented.ReadInbound<IByteBuffer>());
        fragmented.WriteInbound(Unpooled.WrappedBuffer(frame1[1..]));
        Assert.Equal(first, ToArray(fragmented.ReadInbound<IByteBuffer>()!));
    }

    [Fact]
    public void NettyCompression_Above_Threshold_Roundtrip()
    {
        const int threshold = 16;
        var payload = Compressible(2048);

        var encoder = new EmbeddedChannel(new NettyCompressionEncoder(threshold));
        Assert.True(encoder.WriteOutbound(Unpooled.WrappedBuffer(payload)));
        var compressed = encoder.ReadOutbound<IByteBuffer>();
        Assert.NotNull(compressed);
        // 压缩生效：至少因为高重复负载而变小
        Assert.True(compressed!.ReadableBytes < payload.Length);

        compressed.MarkReaderIndex();
        Assert.Equal(payload.Length, compressed.ReadVarIntFromBuffer());
        compressed.ResetReaderIndex();

        var decoder = new EmbeddedChannel(new NettyCompressionDecoder(threshold));
        Assert.True(decoder.WriteInbound(compressed));
        var restored = decoder.ReadInbound<IByteBuffer>();
        Assert.NotNull(restored);
        Assert.Equal(payload, ToArray(restored!));
        Assert.Null(decoder.ReadInbound<IByteBuffer>());
    }

    [Fact]
    public void NettyCompression_Below_Threshold_Is_Passthrough()
    {
        const int threshold = 1024;
        var payload = Compressible(64);

        var encoder = new EmbeddedChannel(new NettyCompressionEncoder(threshold));
        Assert.True(encoder.WriteOutbound(Unpooled.WrappedBuffer(payload)));
        var encoded = encoder.ReadOutbound<IByteBuffer>();
        Assert.NotNull(encoded);
        // 未达阈值：长度前缀 0 + 原文
        encoded!.MarkReaderIndex();
        Assert.Equal(0, encoded.ReadVarIntFromBuffer());
        encoded.ResetReaderIndex();
        Assert.Equal(payload.Length + 1, encoded.ReadableBytes);

        var decoder = new EmbeddedChannel(new NettyCompressionDecoder(threshold));
        Assert.True(decoder.WriteInbound(encoded));
        Assert.Equal(payload, ToArray(decoder.ReadInbound<IByteBuffer>()!));
    }

    [Fact]
    public void NettyEncryption_Encoder_To_Decoder_Roundtrip()
    {
        var key = new byte[16];
        for (var i = 0; i < key.Length; i++) {
            key[i] = (byte)(i + 1);
        }

        var payload = Compressible(333);

        var encoder = new EmbeddedChannel(new NettyEncryptionEncoder(key));
        Assert.True(encoder.WriteOutbound(Unpooled.WrappedBuffer(payload)));
        var encrypted = encoder.ReadOutbound<IByteBuffer>();
        Assert.NotNull(encrypted);
        Assert.Equal(payload.Length, encrypted!.ReadableBytes);
        Assert.NotEqual(payload, ToArray(encrypted.Duplicate()));

        var decoder = new EmbeddedChannel(new NettyEncryptionDecoder(key));
        Assert.True(decoder.WriteInbound(encrypted));
        Assert.Equal(payload, ToArray(decoder.ReadInbound<IByteBuffer>()!));
    }

    [Fact]
    public void PacketManager_Registers_Builtin_Packets()
    {
        var handshake = PacketManager.TriggerEvent(_ => true, EnumConnectionState.Handshake, EnumPacketDirection.ServerBound, 0, EnumProtocolVersion.V1200);
        Assert.IsType<CHandshake>(handshake);

        // 协议版本不匹配时不注册
        Assert.Null(PacketManager.TriggerEvent(_ => true, EnumConnectionState.Login, EnumPacketDirection.ServerBound, 3, EnumProtocolVersion.V1200));
        // 协议版本匹配时注册（1.20.6 的 Login Acknowledged）
        Assert.NotNull(PacketManager.TriggerEvent(_ => true, EnumConnectionState.Login, EnumPacketDirection.ServerBound, 3, EnumProtocolVersion.V1206));
    }

    [Fact]
    public void CHandshake_Rewrites_Address_And_State()
    {
        var config = NewConfig();
        var connection = new GameConnection {
            Config = config
        };

        var buffer = Unpooled.Buffer(64);
        buffer.WriteVarInt((int)EnumProtocolVersion.V1200);
        buffer.WriteStringToBuffer("mc.example.com");
        buffer.WriteShort(25565);
        buffer.WriteVarInt((int)EnumConnectionState.Login);

        var handshake = new CHandshake();
        handshake.ReadFromBuffer(connection, buffer);
        Assert.False(handshake.HandlePacket(connection));

        Assert.Equal(EnumProtocolVersion.V1200, connection.ProtocolVersion);
        Assert.Equal(EnumConnectionState.Login, connection.State);

        var output = Unpooled.Buffer(64);
        handshake.WriteToBuffer(output);
        Assert.Equal((int)EnumProtocolVersion.V1200, output.ReadVarIntFromBuffer());
        Assert.Equal("127.0.0.1\0FML3\0", output.ReadStringFromBuffer());
        Assert.Equal(25565, output.ReadUnsignedShort());
        Assert.Equal((int)EnumConnectionState.Login, output.ReadVarIntFromBuffer());
    }

    [Fact]
    public void UdpBroadcaster_Builds_Motd_Message()
    {
        var config = NewConfig();
        var broadcaster = new Lanw.Development.Utils.UdpBroadcaster(25565, config);
        var build = typeof(Lanw.Development.Utils.UdpBroadcaster).GetMethod("BuildMessage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(build);

        // 1.20 > 1.8.9：带颜色码的 MOTD
        var modern = (string)build!.Invoke(broadcaster, null)!;
        Assert.Equal("[MOTD] §cNirvana §fNirvana Server -> ProxyUser[/MOTD][AD]25565[/AD]", modern);
        broadcaster.Stop();
    }
}
