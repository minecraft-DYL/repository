using System.Text.Json;
using DotNetty.Buffers;
using Lanw.DevPlugin;
using Lanw.DevPlugin.Entities;
using Lanw.DevPlugin.Enums;
using Lanw.DevPlugin.Events;
using Lanw.DevPlugin.Events.Event;
using Lanw.DevPlugin.Packet;
using Lanw.DevPlugin.Plugins;

namespace Lanw.Development.Tests;

/// <summary>带 Plugin 特性的测试插件（用于插件状态机/属性发现验证）。</summary>
[Plugin("lanw.test.plugin", "测试插件", "be-4", "1.2.3", ["lanw.test.dep"])]
public sealed class TestPlugin : IPlugin {
    public static int InitializeCount;

    public void OnInitialize()
    {
        InitializeCount++;
    }
}

/// <summary>不带 Plugin 特性的插件实现（应被属性发现跳过）。</summary>
public sealed class NoAttributePlugin : IPlugin {
    public void OnInitialize() { }
}

/// <summary>测试用数据包（带注册特性）。</summary>
[RegisterPacket(EnumConnectionState.Play, EnumPacketDirection.ClientBound, 0x42, EnumProtocolVersion.V1200)]
public sealed class TestRegisteredPacket : DPacket;

/// <summary>Lanw.DevPlugin（插件系统）移植验证：插件状态机、属性发现、数据包注册与基础包行为。</summary>
public class DevPluginPortTests {
    private sealed class FakeConnection : BGameConnection;

    private sealed class FakeForwardPacket : FPacket;

    private static FakeConnection NewConnection()
    {
        return new FakeConnection {
            Config = new InterceptorConfig {
                IsRental = false,
                LocalPort = 25565,
                NickName = "Dev",
                ForwardAddress = "127.0.0.1",
                ForwardPort = 25565,
                ServerName = "Lanw Server",
                ServerVersion = "1.20",
                ModInfo = string.Empty,
                GameId = "dev-game"
            },
            ProtocolVersion = EnumProtocolVersion.V1200,
            State = EnumConnectionState.Play
        };
    }

    // ---------- 插件状态机 ----------

    [Fact]
    public void PluginState_ShouldDiscoverPluginAttributeAndInstance()
    {
        var plugins = PluginState.CreateAttribute<Plugin, IPlugin>(typeof(DevPluginPortTests).Assembly);

        var pair = Assert.Single(plugins, item => item.Key.Id == "lanw.test.plugin");
        Assert.Equal("测试插件", pair.Key.Name);
        Assert.Equal("be-4", pair.Key.Author);
        Assert.Equal("1.2.3", pair.Key.Version);
        Assert.Equal(new[] { "lanw.test.dep" }, pair.Key.Dependencies);
        Assert.IsType<TestPlugin>(pair.Value);

        // 无 [Plugin] 特性的实现被跳过
        Assert.DoesNotContain(plugins.Values, plugin => plugin is NoAttributePlugin);
    }

    [Fact]
    public void PluginState_ShouldInitializeDiscoveredPlugin()
    {
        TestPlugin.InitializeCount = 0;
        var plugins = PluginState.CreateAttribute<Plugin, IPlugin>(typeof(DevPluginPortTests).Assembly);
        var plugin = plugins.Single(item => item.Key.Id == "lanw.test.plugin").Value;

        plugin.OnInitialize();

        Assert.Equal(1, TestPlugin.InitializeCount);
    }

    [Fact]
    public void PluginState_ShouldReportEnabledStatusFromPath()
    {
        var enabled = new PluginState {
            Assembly = typeof(DevPluginPortTests).Assembly,
            Info = new Plugin("lanw.test.plugin", "测试插件", "be-4", "1.2.3"),
            Md5 = "0123456789abcdef0123456789abcdef",
            Path = "/plugins/lanw.test.plugin.dll",
            Plugin = new TestPlugin()
        };
        var disabled = new PluginState {
            Assembly = typeof(DevPluginPortTests).Assembly,
            Info = enabled.Info,
            Md5 = enabled.Md5,
            Path = "/plugins/lanw.test.plugin.dll.disable",
            Plugin = new TestPlugin()
        };

        var enabledState = enabled.ToPluginStates();
        var disabledState = disabled.ToPluginStates();

        Assert.Equal("1", enabledState.Status);
        Assert.Equal("0", disabledState.Status);
        Assert.Equal("lanw.test.plugin", enabledState.Id);
        Assert.Equal("测试插件", enabledState.Name);
        Assert.Equal("1.2.3", enabledState.Version);
        Assert.Equal("/plugins/lanw.test.plugin.dll", enabledState.Path);
        Assert.Equal(enabledState.Id, disabledState.Id);
    }

    [Fact]
    public void EntityPluginState_ShouldSerializeWithContractNames()
    {
        var json = JsonSerializer.Serialize(new EntityPluginState {
            Id = "id",
            Name = "name",
            Version = "1.0.0",
            Author = "author",
            Status = "1",
            Path = "/plugins/id.dll"
        });

        Assert.Equal("{\"id\":\"id\",\"name\":\"name\",\"version\":\"1.0.0\",\"author\":\"author\",\"status\":\"1\",\"path\":\"/plugins/id.dll\"}", json);
    }

    // ---------- 数据包注册 ----------

    [Fact]
    public void RegisterPacket_ShouldCarryVersionListAndDirection()
    {
        var register = new RegisterPacket(EnumConnectionState.Play, EnumPacketDirection.ServerBound, 5, EnumProtocolVersion.V1200);

        Assert.Equal(EnumConnectionState.Play, register.State);
        Assert.Equal(EnumPacketDirection.ServerBound, register.Direction);
        Assert.Equal(5, register.PacketId);
        Assert.Null(register.GameId);
        Assert.Equal(new[] { EnumProtocolVersion.V1200 }, register.Versions);
    }

    [Fact]
    public void RegisterPacket_ShouldDefaultToAllVersionsWhenVersionOmitted()
    {
        var register = new RegisterPacket(EnumConnectionState.Login, EnumPacketDirection.ClientBound, -1);

        Assert.Equal(new[] { EnumProtocolVersion.All }, register.Versions);
        Assert.Equal(-1, register.PacketId);
        Assert.Null(register.GameId);
    }

    [Fact]
    public void RegisterPacket_ShouldSupportGameIdAndMultipleVersions()
    {
        var register = new RegisterPacket(
            EnumConnectionState.Play,
            EnumPacketDirection.ClientBound,
            7,
            "game-1",
            EnumProtocolVersion.V1180,
            EnumProtocolVersion.V1200);

        Assert.Equal("game-1", register.GameId);
        Assert.Equal(new[] { EnumProtocolVersion.V1180, EnumProtocolVersion.V1200 }, register.Versions);
    }

    [Fact]
    public void RegisterPacket_ShouldBeDiscoverableOnPacketClass()
    {
        var attribute = (RegisterPacket?)Attribute.GetCustomAttribute(typeof(TestRegisteredPacket), typeof(RegisterPacket));

        Assert.NotNull(attribute);
        Assert.Equal(0x42, attribute.PacketId);
        Assert.Equal(EnumConnectionState.Play, attribute.State);
        Assert.Equal(EnumPacketDirection.ClientBound, attribute.Direction);
        Assert.Equal(new[] { EnumProtocolVersion.V1200 }, attribute.Versions);
    }

    [Fact]
    public void RegisterEvent_ShouldDefaultToAllVersions()
    {
        Assert.Equal(EnumProtocolVersion.All, new RegisterEvent().Version);
        Assert.Equal(EnumProtocolVersion.V108X, new RegisterEvent(EnumProtocolVersion.V108X).Version);
    }

    // ---------- 基础包行为 ----------

    [Fact]
    public void ForwardPacket_ShouldWriteBackRawBytesUnchanged()
    {
        var connection = NewConnection();
        var input = Unpooled.Buffer();
        input.WriteBytes([0x01, 0x02, 0x03]);

        var packet = new FakeForwardPacket();
        packet.ReadFromBuffer(connection, input);

        var output = Unpooled.Buffer();
        packet.WriteToBuffer(output);

        var bytes = new byte[output.ReadableBytes];
        output.ReadBytes(bytes);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, bytes);
        // FPacket 用 GetBytes 抓取，读取位置不动
        Assert.Equal(3, input.ReadableBytes);
    }

    [Fact]
    public void ForwardPacket_ShouldWriteNothingBeforeRead()
    {
        var output = Unpooled.Buffer();
        new FakeForwardPacket().WriteToBuffer(output);

        Assert.Equal(0, output.ReadableBytes);
    }

    [Fact]
    public void DataPacketAndDefaultHandle_ShouldBeNoOps()
    {
        var connection = NewConnection();
        var packet = new TestRegisteredPacket();
        var input = Unpooled.Buffer();
        input.WriteByte(0x7F);

        packet.ReadFromBuffer(connection, input);
        var output = Unpooled.Buffer();
        packet.WriteToBuffer(output);

        Assert.Equal(0, output.ReadableBytes);
        Assert.False(packet.HandlePacket(connection));
        // DPacket.ReadFromBuffer 为空实现，读取位置不动
        Assert.Equal(0x7F, input.ReadByte());
    }

    [Fact]
    public void PluginMessageEvent_ShouldDeclarePayloadAndCancelContract()
    {
        IEventPluginMessage message = new FakePluginMessageEvent {
            Identifier = "lanw:test",
            Payload = [1, 2]
        };

        Assert.Equal("lanw:test", message.Identifier);
        Assert.Equal(new byte[] { 1, 2 }, message.Payload);
        Assert.True(message.OnPluginMessage(NewConnection()));
    }

    private sealed class FakePluginMessageEvent : IEventPluginMessage {
        public string? Identifier { get; set; }
        public byte[]? Payload { get; set; }
    }
}
