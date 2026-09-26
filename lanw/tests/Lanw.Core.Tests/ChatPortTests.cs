using System.Text;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Lanw.Chat;
using Lanw.Chat.Connection;
using Lanw.Chat.Entities;
using Lanw.Chat.Entities.Table;
using Lanw.Chat.Enums;
using Lanw.Chat.Extensions;
using Lanw.Chat.Manager;
using Lanw.Chat.Packet.V108X.Chat;
using Lanw.Chat.Packet.V1122.Chat;
using Lanw.Chat.Packet.V1180.Chat;
using Lanw.Chat.Packet.V1200.Chat;
using Lanw.Chat.Packet.IPacket;
using Lanw.Chat.Utils;

namespace Lanw.Core.Tests;

/// <summary>IRC 跨服聊天室后端移植测试。</summary>
public class ChatPortTests {
    /// <summary>测试用游戏连接（仅实现聊天室依赖的连接契约）。</summary>
    private sealed class FakeGameConnection(string gameId, string nickName, EnumProtocolVersion version) : IGameConnection {
        public string GameId { get; } = gameId;
        public string NickName { get; } = nickName;
        public EnumProtocolVersion ProtocolVersion { get; } = version;
        public IChannel ClientChannel { get; } = null!;
    }

    // ---------- MinecraftColorCodeConverter ----------

    [Fact]
    public void ColorCode_ShouldConvertSingleColorSegment()
    {
        var message = MinecraftColorCodeConverter.ParseColoredString("§aHello");

        Assert.Equal(string.Empty, message.Text);
        var part = Assert.Single(message.Extra);
        Assert.Equal("green", part.Color);
        Assert.Equal("Hello", part.Text);
    }

    [Fact]
    public void ColorCode_ShouldSplitMultipleColorsInOrder()
    {
        var message = MinecraftColorCodeConverter.ParseColoredString("§aHello §bWorld §6!");

        Assert.Equal(3, message.Extra.Count);
        Assert.Equal(("green", "Hello "), (message.Extra[0].Color, message.Extra[0].Text));
        Assert.Equal(("aqua", "World "), (message.Extra[1].Color, message.Extra[1].Text));
        Assert.Equal(("gold", "!"), (message.Extra[2].Color, message.Extra[2].Text));
    }

    [Fact]
    public void ColorCode_ShouldMapResetToWhiteAndIgnoreUnknownCode()
    {
        var reset = MinecraftColorCodeConverter.ParseColoredString("§rReset");
        var part = Assert.Single(reset.Extra);
        Assert.Equal("white", part.Color);
        Assert.Equal("Reset", part.Text);

        // §z 不是有效颜色码：不会开启颜色，因此不会产出任何文本段
        var unknown = MinecraftColorCodeConverter.ParseColoredString("§zNothing");
        Assert.Empty(unknown.Extra);
    }

    [Fact]
    public void ColorCode_ShouldDropTextBeforeFirstColorCode()
    {
        var message = MinecraftColorCodeConverter.ParseColoredString("prefix §cRed");

        var part = Assert.Single(message.Extra);
        Assert.Equal("red", part.Color);
        Assert.Equal("Red", part.Text);
    }

    [Fact]
    public void ColorCode_ShouldProduceEmptyExtraForPlainText()
    {
        var message = MinecraftColorCodeConverter.ParseColoredString("no color here");

        Assert.Empty(message.Extra);
        Assert.Equal(string.Empty, message.Text);
    }

    [Fact]
    public void ColorCode_MessageShouldSerializeToMinecraftJsonShape()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(MinecraftColorCodeConverter.ParseColoredString("§bSky"));

        Assert.Contains("\"extra\"", json);
        Assert.Contains("\"color\":\"aqua\"", json);
        Assert.Contains("\"text\":\"Sky\"", json);
    }

    // ---------- Utf8ByteReplacer ----------

    [Fact]
    public void Utf8_FindIndexShouldLocateAsciiNeedle()
    {
        var haystack = Encoding.UTF8.GetBytes("hello fengheng1314 world");

        Assert.Equal(6, Utf8ByteReplacer.FindIndex(haystack, "fengheng1314"));
        Assert.Equal(-1, Utf8ByteReplacer.FindIndex(haystack, "missing"));
        Assert.Equal(-1, Utf8ByteReplacer.FindIndex(haystack, string.Empty));
    }

    [Fact]
    public void Utf8_FindIndexShouldLocateMultibyteNeedle()
    {
        var haystack = Encoding.UTF8.GetBytes("玩家 §a小明 加入了游戏");

        // "小明" 之前是 "玩家 §a" = 2*3 + 1 + 2 + 1 = 10 字节
        Assert.Equal(10, Utf8ByteReplacer.FindIndex(haystack, "小明"));
        Assert.Equal(0, Utf8ByteReplacer.FindIndex(haystack, "玩家"));
    }

    [Fact]
    public void Utf8_FindIndexShouldRejectLongerNeedleThanHaystack()
    {
        var haystack = Encoding.UTF8.GetBytes("ab");

        Assert.Equal(-1, Utf8ByteReplacer.FindIndex(haystack, "abcdef"));
        Assert.Equal(-1, Utf8ByteReplacer.FindIndex([], "ab"));
    }

    [Fact]
    public void Utf8_ReplaceAtShouldReplaceSameLengthContent()
    {
        var data = Encoding.UTF8.GetBytes("hello fengheng1314 world");
        var index = Utf8ByteReplacer.FindIndex(data, "fengheng1314");

        var replaced = Utf8ByteReplacer.ReplaceAt(data, index, "fengheng1314".Length, "asdadsadas123");

        Assert.Equal("hello asdadsadas123 world", Encoding.UTF8.GetString(replaced));
    }

    [Fact]
    public void Utf8_ReplaceAtShouldSupportMultibyteAndLengthChange()
    {
        var data = Encoding.UTF8.GetBytes("玩家小明加入了游戏");
        var index = Utf8ByteReplacer.FindIndex(data, "小明");

        var replaced = Utf8ByteReplacer.ReplaceAt(data, index, Encoding.UTF8.GetByteCount("小明"), "小红来了");

        Assert.Equal("玩家小红来了加入了游戏", Encoding.UTF8.GetString(replaced));
        Assert.Equal(Encoding.UTF8.GetByteCount("玩家小红来了加入了游戏"), replaced.Length);
    }

    [Fact]
    public void Utf8_ReplaceAtShouldReturnOriginalWhenIndexInvalid()
    {
        var data = Encoding.UTF8.GetBytes("abc");

        Assert.Same(data, Utf8ByteReplacer.ReplaceAt(data, -1, 1, "x"));
        Assert.Same(data, Utf8ByteReplacer.ReplaceAt(data, 0, 4, "x"));
        Assert.Same(data, Utf8ByteReplacer.ReplaceAt(data, 3, 1, "x"));
    }

    // ---------- IByteBuffer 扩展（VarInt / 字符串 / 剩余字节） ----------

    [Fact]
    public void BufferExtensions_ShouldRoundTripVarIntAndString()
    {
        var buffer = Unpooled.Buffer();

        buffer.WriteVarInt(300);
        buffer.WriteVarInt(0);
        buffer.WriteStringToBuffer("§a跨服");
        buffer.WriteByteArrayToBuffer([1, 2, 3]);

        Assert.Equal(300, buffer.ReadVarIntFromBuffer());
        Assert.Equal(0, buffer.ReadVarIntFromBuffer());
        Assert.Equal("§a跨服", buffer.ReadStringFromBuffer());
        // ReadByteArrayReadableBytes 读取剩余全部字节，因此仍包含字节数组的 VarInt 长度前缀
        Assert.Equal(new byte[] { 3, 1, 2, 3 }, buffer.ReadByteArrayReadableBytes());
        Assert.Equal(0, buffer.ReadableBytes);
    }

    [Fact]
    public void BufferExtensions_ShouldReadRawRemainingBytes()
    {
        var buffer = Unpooled.Buffer();
        buffer.WriteBytes([1, 2, 3]);

        Assert.Equal(new byte[] { 1, 2, 3 }, buffer.ReadByteArrayReadableBytes());
        Assert.Equal(0, buffer.ReadableBytes);
        Assert.Empty(Unpooled.Buffer().ReadByteArrayReadableBytes());
    }

    [Fact]
    public void BufferExtensions_ReadStringShouldClampToReadableBytes()
    {
        var buffer = Unpooled.Buffer();
        buffer.WriteVarInt(16);
        buffer.WriteBytes(Encoding.UTF8.GetBytes("abc"));

        Assert.Equal("abc", buffer.ReadStringFromBuffer());
    }

    // ---------- 各版本聊天包元信息 ----------

    [Fact]
    public void ChatPackets_ShouldCarryExpectedProtocolMetadata()
    {
        APacket[] packets = [
            new C01PacketChatMessage(),
            new CPacketChatMessage(),
            new Lanw.Chat.Packet.V1180.Chat.ServerboundChatPacket(),
            new Lanw.Chat.Packet.V1200.Chat.ServerboundChatCommandPacket(),
            new Lanw.Chat.Packet.V1206.Chat.ServerboundChatCommandPacket()
        ];

        Assert.Equal(new[] { 1, 2, 3, 4, 4 }, packets.Select(p => p.PacketId).ToArray());
        Assert.Equal(
            new[] { EnumProtocolVersion.V108X, EnumProtocolVersion.V1122, EnumProtocolVersion.V1180, EnumProtocolVersion.V1200, EnumProtocolVersion.V1206 },
            packets.Select(p => p.ProtocolVersion).ToArray());
        Assert.All(packets, p => Assert.Equal(EnumConnectionState.Play, p.State));
        Assert.All(packets, p => Assert.Equal(EnumPacketDirection.ServerBound, p.Direction));
    }

    [Fact]
    public void IrcPacketHandler_ShouldSuppressIrcCommandOnWhiteSide()
    {
        var packet = new C01PacketChatMessage();
        var input = Unpooled.Buffer();
        input.WriteStringToBuffer("/irc hello");

        packet.ReadFromBuffer(input);

        var output = Unpooled.Buffer();
        packet.WriteToBuffer(output);

        // IRC 指令被拦截：不再向白端转发原始包
        Assert.Equal(0, output.ReadableBytes);
    }

    [Fact]
    public void IrcPacketHandler_ShouldPassThroughNormalChat()
    {
        var packet = new CPacketChatMessage();
        var input = Unpooled.Buffer();
        input.WriteStringToBuffer("hello world");

        packet.ReadFromBuffer(input);

        var output = Unpooled.Buffer();
        packet.WriteToBuffer(output);

        Assert.True(output.ReadableBytes > 0);
        Assert.Equal("hello world", output.ReadStringFromBuffer());
    }

    // ---------- 实体（IRC 报文） ----------

    [Fact]
    public void EntityChat_ShouldSerializeWithModeAndMessage()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new EntityChat {
            Message = "hi"
        });

        Assert.Equal("{\"mode\":\"chat\",\"message\":\"hi\"}", json);
    }

    [Fact]
    public void EntityChatJoin_ShouldMatchGameConnection()
    {
        var join = new EntityChatJoin {
            GameId = "12",
            NickName = "Steve"
        };
        var connection = new FakeGameConnection("12", "Steve", EnumProtocolVersion.V1200);

        Assert.Equal("join", join.Mode);
        Assert.True(join.Equals(connection));
        Assert.False(join.Equals(new FakeGameConnection("12", "Alex", EnumProtocolVersion.V1200)));
        Assert.False(join.Equals(new EntityChatJoin {
            GameId = "13",
            NickName = "Steve"
        }));

        join.Mode = "removeJoin";
        Assert.Equal("removeJoin", join.Mode);
    }

    [Fact]
    public void EntityChatConfig_ShouldReturnRandomHeartbeatOrEmpty()
    {
        var config = new EntityChatConfig();
        Assert.Equal(string.Empty, config.GetHeartbeat());

        config.Heartbeats.Add("beat");
        Assert.Equal("beat", config.GetHeartbeat());
        Assert.Equal("beat", config.GetHeartbeat());
    }

    [Fact]
    public void EntityChatConfig_ShouldDeserializePlayers()
    {
        const string json = "{\"heartbeats\":[\"a\"],\"players\":[{\"account\":\"acc\",\"players\":[{\"nickName\":\"Steve\",\"gameId\":\"7\"}]}]}";

        var config = System.Text.Json.JsonSerializer.Deserialize<EntityChatConfig>(json);

        Assert.NotNull(config);
        var player = Assert.Single(config.Players);
        Assert.Equal("acc", player.Account);
        var join = Assert.Single(player.Players);
        Assert.Equal("Steve", join.NickName);
        Assert.Equal("7", join.GameId);
    }

    // ---------- 玩家列表（Tab）实体 ----------

    [Fact]
    public void EntityTabAdd_ShouldParseAndRenameDisplayName()
    {
        var input = Unpooled.Buffer();
        input.WriteLong(1L);
        input.WriteLong(2L);
        input.WriteStringToBuffer("Steve");
        input.WriteVarInt(0); // 无签名目标
        input.WriteVarInt(0); // gameMode
        input.WriteVarInt(5); // ping
        input.WriteBoolean(true);
        input.WriteStringToBuffer("Old_Steve");
        input.WriteByte(0xAB); // 尾部剩余字节

        var entity = new EntityTabAdd(input);
        Assert.Equal("Steve", entity.Name);

        entity.OldName = "Old";
        entity.NewName = "New";
        var output = Unpooled.Buffer();
        entity.WriteToBuffer(output);

        Assert.Equal(1L, output.ReadLong());
        Assert.Equal(2L, output.ReadLong());
        Assert.Equal("Steve", output.ReadStringFromBuffer());
        Assert.Equal(0, output.ReadVarIntFromBuffer()); // targets
        Assert.Equal(0, output.ReadVarIntFromBuffer()); // gameMode
        Assert.Equal(5, output.ReadVarIntFromBuffer()); // ping
        Assert.True(output.ReadBoolean());
        Assert.Equal("New_Steve", output.ReadStringFromBuffer());
        Assert.Equal(0xAB, output.ReadByte());
        Assert.Equal(0, output.ReadableBytes);
    }

    [Fact]
    public void EntityTabAdd_ShouldEmitJsonTextWhenDisplayNameAbsent()
    {
        var input = Unpooled.Buffer();
        input.WriteLong(1L);
        input.WriteLong(2L);
        input.WriteStringToBuffer("Steve");
        input.WriteVarInt(0);
        input.WriteVarInt(0);
        input.WriteVarInt(0);
        input.WriteBoolean(false);

        var entity = new EntityTabAdd(input);
        entity.OldName = "Old";
        entity.NewName = "New";

        var output = Unpooled.Buffer();
        entity.WriteToBuffer(output);
        output.ReadLong();
        output.ReadLong();
        output.ReadStringFromBuffer();
        output.ReadVarIntFromBuffer();
        output.ReadVarIntFromBuffer();
        output.ReadVarIntFromBuffer();
        Assert.True(output.ReadBoolean());
        Assert.Contains("\"text\":\"New\"", output.ReadStringFromBuffer());
    }

    [Fact]
    public void EntityTabUpdate_ShouldParseAndRename()
    {
        var input = Unpooled.Buffer();
        input.WriteLong(9L);
        input.WriteLong(8L);
        input.WriteBoolean(true);
        input.WriteStringToBuffer("A_Steve");
        input.WriteByte(0x01);

        var entity = new EntityTabUpdate(input);
        entity.OldName = "A";
        entity.NewName = "B";

        var output = Unpooled.Buffer();
        entity.WriteToBuffer(output);

        Assert.Equal(9L, output.ReadLong());
        Assert.Equal(8L, output.ReadLong());
        Assert.True(output.ReadBoolean());
        Assert.Equal("B_Steve", output.ReadStringFromBuffer());
        Assert.Equal(0x01, output.ReadByte());
        Assert.Equal(0, output.ReadableBytes);
    }

    [Fact]
    public void EntityTabTarget_ShouldRoundTripNameValueAndSignature()
    {
        var input = Unpooled.Buffer();
        input.WriteStringToBuffer("name");
        input.WriteStringToBuffer("value");
        input.WriteBoolean(true);
        input.WriteStringToBuffer("sign");

        var target = new EntityTabTarget(input);
        var output = Unpooled.Buffer();
        target.WriteToBuffer(output);

        Assert.Equal("name", output.ReadStringFromBuffer());
        Assert.Equal("value", output.ReadStringFromBuffer());
        Assert.True(output.ReadBoolean());
        Assert.Equal("sign", output.ReadStringFromBuffer());
        Assert.Equal(0, output.ReadableBytes);
    }

    [Fact]
    public void EntityTabTarget_ShouldRoundTripWithoutSignature()
    {
        var input = Unpooled.Buffer();
        input.WriteStringToBuffer("name");
        input.WriteStringToBuffer("value");
        input.WriteBoolean(false);

        var target = new EntityTabTarget(input);
        var output = Unpooled.Buffer();
        target.WriteToBuffer(output);

        Assert.Equal("name", output.ReadStringFromBuffer());
        Assert.Equal("value", output.ReadStringFromBuffer());
        Assert.False(output.ReadBoolean());
        Assert.Equal(0, output.ReadableBytes);
    }

    // ---------- 插件入口与注册 ----------

    [Fact]
    public void ChatPluginMain_ShouldRegisterAllPacketsAndHandlers()
    {
        EventManager.Instance.Clear();

        ChatPluginMain.Initialize();

        var registered = NPacketManager.RegisteredPackets.Where(item => item.PluginName == "Lanw.Chat").ToList();
        Assert.Equal(5, registered.Count);
        Assert.Contains(registered, item => item.Packet is C01PacketChatMessage);
        Assert.Contains(registered, item => item.Packet is Lanw.Chat.Packet.V1122.Chat.CPacketChatMessage);
        Assert.Contains(registered, item => item.Packet is Lanw.Chat.Packet.V1180.Chat.ServerboundChatPacket);
        Assert.Contains(registered, item => item.Packet is Lanw.Chat.Packet.V1200.Chat.ServerboundChatCommandPacket);
        Assert.Contains(registered, item => item.Packet is Lanw.Chat.Packet.V1206.Chat.ServerboundChatCommandPacket);

        // ChatManager.Register 已在连接通道上注册断开处理器（连接未建立时 RemoveJoin 安全返回）
        EventManager.Instance.Trigger(MessageChannels.ChannelConnection, new Lanw.Chat.Events.EventConnectionClosed(
            new FakeGameConnection("-1", "Steve", EnumProtocolVersion.V108X)));

        EventManager.Instance.Clear();
    }
}
