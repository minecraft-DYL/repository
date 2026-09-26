using System.Text;
using Lanw.Game.Launcher.Entities;
using Lanw.Game.Launcher.Entities.WPFLauncher.Launch.RPC;
using Lanw.Game.Launcher.Services.Java.RPC.Events;
using Lanw.Game.Launcher.Utils;

namespace Lanw.Core.Tests;

/// <summary>
/// 游戏启动器（Lanw.Game.Launcher）参数生成 / 版本转换 / RPC 报文 移植验证。
/// </summary>
public class GameLauncherPortTests {
    // ── GameArgumentsUtil：参数拼接 ──────────────────────────────────────────

    [Fact]
    public void AddArguments_ShouldAppendWithSingleSpace()
    {
        Assert.Equal("--version 1.20 -Xmx2G", GameArgumentsUtil.AddArguments("--version 1.20", "-Xmx2G"));
    }

    [Fact]
    public void AddArguments_ShouldNotDuplicateTrailingSpace()
    {
        Assert.Equal("--version -Xmx2G", GameArgumentsUtil.AddArguments("--version ", "-Xmx2G"));
    }

    // ── GameArgumentsUtil：参数读取 ──────────────────────────────────────────

    [Fact]
    public void GetArguments_Mode12_ShouldReadQuotedValue()
    {
        const string text = " -cp \"a.jar;b.jar\" -Dfoo=bar";

        Assert.Equal("a.jar;b.jar", GameArgumentsUtil.GetArguments("cp", text, false, CommandMode.Mode12, CommandMode.Mode13));
        Assert.Equal(" -cp \"a.jar;b.jar\"", GameArgumentsUtil.GetArguments("cp", text, true, CommandMode.Mode12, CommandMode.Mode13));
    }

    [Fact]
    public void GetArguments_Mode3_ShouldReadSpaceSeparatedValue()
    {
        Assert.Equal("25565", GameArgumentsUtil.GetArguments("port", " --port 25565 --foo", false, CommandMode.Mode3, CommandMode.Mode13));
    }

    [Fact]
    public void GetArguments_WhenMissing_ShouldReturnEmpty()
    {
        Assert.Equal(string.Empty, GameArgumentsUtil.GetArguments("cp", "--foo bar", true, CommandMode.Mode12, CommandMode.Mode13));
    }

    // ── GameArgumentsUtil：参数替换 / 删除 ───────────────────────────────────

    [Fact]
    public void UpdateArguments_ShouldReplaceExistingValue()
    {
        const string text = " -cp \"a.jar;b.jar\" -Dfoo=bar";

        Assert.Equal(" -cp \"c.jar\" -Dfoo=bar",
            GameArgumentsUtil.UpdateArguments("cp", "c.jar", text, CommandMode.Mode12, CommandMode.Mode13));
    }

    [Fact]
    public void UpdateArguments_WhenMissing_ShouldAppendGeneratedArguments()
    {
        // 参考实现：缺失时不删除任何内容，直接在末尾追加生成参数（生成参数自带前导空格）
        Assert.Equal("--foo bar  -cp \"c.jar\"",
            GameArgumentsUtil.UpdateArguments("cp", "c.jar", "--foo bar", CommandMode.Mode12, CommandMode.Mode13));
    }

    [Fact]
    public void UpdateArguments_EmptyValue_ShouldAppendNothing()
    {
        Assert.Equal("--foo bar",
            GameArgumentsUtil.UpdateArguments("cp", string.Empty, "--foo bar", CommandMode.Mode12, CommandMode.Mode13));
    }

    [Fact]
    public void DeleteArguments_ShouldRemoveArgument()
    {
        Assert.Equal(" -Dfoo=bar",
            GameArgumentsUtil.DeleteArguments("cp", " -cp \"a.jar;b.jar\" -Dfoo=bar", CommandMode.Mode12, CommandMode.Mode13));
    }

    [Fact]
    public void GetArguments_UnsupportedMode_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GameArgumentsUtil.GetArguments("cp", " -cp \"a\"", true, (CommandMode)99));
    }

    // ── GameVersionConverter：版本号 → 枚举 ──────────────────────────────────

    [Fact]
    public void GameVersionConverter_ShouldMatchByEnumValue()
    {
        // 参考实现：以“枚举数值”匹配（Dictionary 的 key 仅用于可读性）
        Assert.Equal(EnumGameVersion.V_1_8, GameVersionConverter.Convert(1008000));
        Assert.Equal(EnumGameVersion.V_1_18, GameVersionConverter.Convert(1018000));
        Assert.Equal(EnumGameVersion.V_1_21_8, GameVersionConverter.Convert(1021008));
        Assert.Equal(EnumGameVersion.V_CPP, GameVersionConverter.Convert(100000000));
    }

    [Fact]
    public void GameVersionConverter_ShouldFallbackToNone()
    {
        Assert.Equal(EnumGameVersion.NONE, GameVersionConverter.Convert(0));
        Assert.Equal(EnumGameVersion.NONE, GameVersionConverter.Convert(1));
        Assert.Equal(EnumGameVersion.NONE, GameVersionConverter.Convert(999999999));
    }

    // ── GameVersionUtil：枚举 ↔ 版本名 ───────────────────────────────────────

    [Fact]
    public void GameVersionUtil_ShouldConvertGameVersionFromEnum()
    {
        Assert.Equal("1.20.6", GameVersionUtil.GetGameVersionFromEnum(EnumGameVersion.V_1_20_6));
        Assert.Equal("1.7.10", GameVersionUtil.GetGameVersionFromEnum(EnumGameVersion.V_1_7_10));
        Assert.Equal("NONE", GameVersionUtil.GetGameVersionFromEnum(EnumGameVersion.NONE));
    }

    [Fact]
    public void GameVersionUtil_ShouldConvertEnumFromGameVersion()
    {
        Assert.Equal(EnumGameVersion.V_1_12_2, GameVersionUtil.GetEnumFromGameVersion("1.12.2"));
        Assert.Equal(EnumGameVersion.V_1_8_9, GameVersionUtil.GetEnumFromGameVersion("1.8.9"));
        Assert.Equal(EnumGameVersion.NONE, GameVersionUtil.GetEnumFromGameVersion("9.9.9"));
    }

    // ── RPC 报文：SimplePack / SimpleUnpack ─────────────────────────────────

    [Fact]
    public void SimplePack_ShouldWriteUshortPrefixAndUtf8String()
    {
        var data = SimplePack.Pack((ushort)512, "hi");

        Assert.NotNull(data);
        Assert.Equal(6, data!.Length);
        Assert.Equal((ushort)512, BitConverter.ToUInt16(data, 0));
        Assert.Equal((ushort)2, BitConverter.ToUInt16(data, 2)); // 字符串长度前缀
        Assert.Equal("hi", Encoding.UTF8.GetString(data, 4, 2));
    }

    [Fact]
    public void SimplePack_ShouldWritePrimitiveList()
    {
        var data = SimplePack.Pack(new List<uint> { 1u, 2u });

        Assert.NotNull(data);
        Assert.Equal(10, data!.Length); // 2 字节总长度 + 2 * 4 字节
        Assert.Equal((ushort)8, BitConverter.ToUInt16(data, 0));
        Assert.Equal(1u, BitConverter.ToUInt32(data, 2));
        Assert.Equal(2u, BitConverter.ToUInt32(data, 6));
    }

    [Fact]
    public void SimplePack_ShouldReturnNullForNullInput()
    {
        Assert.Null(SimplePack.Pack(null));
    }

    [Fact]
    public void SimplePack_UnsupportedType_ShouldThrow()
    {
        Assert.Throws<NotSupportedException>(() => SimplePack.Pack(new object()));
    }

    [Fact]
    public void SimpleUnpack_ShouldReadOtherEnterWorldMessage()
    {
        var name = Encoding.UTF8.GetBytes("abc");
        var uuid = Encoding.UTF8.GetBytes("de");
        var buffer = SimplePack.Pack((short)1, (ushort)name.Length, name, (ushort)uuid.Length, uuid);

        var msg = new EntityOtherEnterWorldMsg();
        new SimpleUnpack(buffer!).Unpack(ref msg);

        Assert.Equal((short)1, msg.Id);
        Assert.Equal((ushort)3, msg.Length);
        Assert.Equal("abc", msg.Name);
        Assert.Equal((ushort)2, msg.UuidLength);
        Assert.Equal("de", msg.Uuid);
    }

    // ── EntityJavaFile / CommandService：路径与系统判定 ─────────────────────

    [Fact]
    public void EntityJavaFile_ShouldNormalizeSeparatorsAndRelativePath()
    {
        var file = new EntityJavaFile("libraries\\org/lwjgl/lwjgl.jar", "https://example.com/a.jar", "org.lwjgl:lwjgl:3.3.1");

        Assert.EndsWith("lwjgl.jar", file.GetPath1()); // 分隔符已统一
        Assert.StartsWith(Lanw.Core.Utils.PathUtil.GameBaseMcPath, file.GetPath()); // 相对路径拼到 .minecraft
        Assert.True(file.Contains("lwjgl.jar"));
        Assert.False(file.IsNative());
    }

    [Fact]
    public void EntityJavaFile_ShouldDetectNativeByClassifier()
    {
        var runOs = Lanw.Game.Launcher.Services.Java.CommandService.GetRunOs()[0];
        var file = new EntityJavaFile("libraries/a.jar", "https://example.com/a.jar", $"org.lwjgl:lwjgl-glfw:3.3.1:natives-{runOs}");

        Assert.True(file.IsNative());
    }

    [Fact]
    public void CommandService_GetRunOs_ShouldReturnCurrentPlatform()
    {
        var os = Lanw.Game.Launcher.Services.Java.CommandService.GetRunOs();

        Assert.NotEmpty(os);
        if (OperatingSystem.IsWindows()) {
            Assert.Equal(new[] { "windows" }, os);
        } else if (OperatingSystem.IsMacOS()) {
            Assert.Equal(new[] { "osx", "macos" }, os);
        } else {
            Assert.Equal(new[] { "linux" }, os);
        }
    }
}
