using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lanw.Core.Utils;
using Lanw.Development.Manager;
using Lanw.Heypixel;
using Lanw.Heypixel.Configuration;
using Lanw.Public.Entities.NEL;
using Lanw.Public.Entities.Update;
using Lanw.Public.Manager;
using Lanw.Public.Utils;
using Lanw.Public.Utils.ViewLogger;
using Serilog.Events;
using Serilog.Parsing;

namespace Lanw.Core.Tests;

// 校验 Nirvana.Public 杂项后端 → Lanw 移植后的行为
// （ViewLogger 内存日志 / UpdateTools 文件更新 / NirvanaAccountManager 支撑 / NEL 代理编排）。
public class MiscPortTests
{
    // --- ViewLogger: InMemorySink（日志页实时读取） ---

    private static LogEvent CreateEvent(string message)
    {
        return new LogEvent(
            DateTimeOffset.Now,
            LogEventLevel.Information,
            null,
            new MessageTemplateParser().Parse(message),
            []);
    }

    [Fact]
    public void InMemorySink_Emit_FormatsLevelAndMessage()
    {
        var id = Guid.NewGuid().ToString("N");
        InMemorySink.Instance.Emit(CreateEvent("t17 " + id));

        var logs = InMemorySink.GetLogs().ToList();
        Assert.Contains($"[Information] t17 {id}", logs);
    }

    [Fact]
    public void InMemorySink_GetLogs_ReturnsOldestFirst()
    {
        var first = Guid.NewGuid().ToString("N");
        var second = Guid.NewGuid().ToString("N");
        InMemorySink.Instance.Emit(CreateEvent("t17-a " + first));
        InMemorySink.Instance.Emit(CreateEvent("t17-b " + second));

        var mine = InMemorySink.GetLogs().Where(log => log.Contains(first) || log.Contains(second)).ToList();
        Assert.Equal(2, mine.Count);
        Assert.Contains(first, mine[0]);
        Assert.Contains(second, mine[1]);
    }

    [Fact]
    public void InMemorySink_IsSharedSingleton()
    {
        Assert.Same(InMemorySink.Instance, InMemorySink.Instance);
        Assert.IsAssignableFrom<Serilog.Core.ILogEventSink>(InMemorySink.Instance);
    }

    // --- ViewLogger: CustomConsoleTheme / Logger 配置 ---

    [Fact]
    public void CustomConsoleTheme_DoesNotBufferAndWritesNothing()
    {
        var theme = new CustomConsoleTheme(ConsoleColor.Red);
        Assert.False(theme.CanBuffer);
        // Set 只改控制台前景色，始终返回 0（与参考实现一致）
        Assert.Equal(0, theme.Set(Console.Out, Serilog.Sinks.SystemConsole.Themes.ConsoleThemeStyle.SecondaryText));
        Assert.Equal(0, theme.Set(Console.Out, Serilog.Sinks.SystemConsole.Themes.ConsoleThemeStyle.Text));
        theme.Reset(Console.Out);
    }

    [Fact]
    public void Logger_IsSerilogConfiguration()
    {
        var logger = new Logger();
        Assert.IsAssignableFrom<Serilog.LoggerConfiguration>(logger);
    }

    // --- RestartTools: 启动参数解析 ---

    [Fact]
    public void RestartTools_Get_ReadsArgumentAndDefault()
    {
        var args = new[] { "--mode", "proxy", "--port", "25566" };
        Assert.Equal("proxy", RestartTools.Get("mode", args));
        Assert.Equal(25566, RestartTools.Get<int>("port", args));
        Assert.Equal("nirvana", RestartTools.Get("default_skin_id", args, "nirvana"));
        Assert.Equal(-1, RestartTools.Get("MainPid", args, -1));
    }

    [Fact]
    public void RestartTools_Get_WithoutDefault_Throws()
    {
        Assert.Throws<Exception>(() => RestartTools.Get("missing", ["--mode", "proxy"], null));
    }

    // --- EntityUpdateFile / EntityUpdate: 更新实体 ---

    [Fact]
    public void EntityUpdateFile_GetPath_CombinesUpdaterBaseAndBasePathList()
    {
        var node = JsonNode.Parse("""
        {
            "url": "https://example.com/a.zip",
            "size": 3,
            "sha256": "00",
            "path": "sub\\dir\\file.bin"
        }
        """);
        var item = new EntityUpdateFile(node) { Index = 7 };
        Assert.Equal(7, item.Index);

        // 非安全模式：UpdaterBasePath + basePathList + FilePath
        Assert.Equal(
            Path.Combine(PathUtil.UpdaterBasePath, "extra", "sub", "dir", "file.bin"),
            item.GetPath(false, "extra"));

        // 安全模式：UpdaterPath + FilePath（无附加基础路径）
        Assert.Equal(
            Path.Combine(PathUtil.UpdaterPath, "sub", "dir", "file.bin"),
            item.GetPath(true));
    }

    [Fact]
    public void EntityUpdateFile_GetPath_ReturnsNullWithoutPathNode()
    {
        var item = new EntityUpdateFile(JsonNode.Parse("{}")) { Index = 0 };
        Assert.Null(item.GetPath(false));

        var empty = new EntityUpdateFile(null) { Index = 0 };
        Assert.Null(empty.GetPath(false));
    }

    [Fact]
    public async Task EntityUpdateFile_CheckUpdate_WithoutDownloadUrl_ReturnsFatalError()
    {
        var node = JsonNode.Parse("""{ "path": "a.bin" }""");
        var item = new EntityUpdateFile(node) { Index = 0 };

        // 参考实现：无下载地址直接返回 2（致命错误），不发任何网络请求
        Assert.Equal(2, await item.CheckUpdate("missing.bin", "missing.bin"));
    }

    [Fact]
    public void EntityUpdate_Defaults_MatchReference()
    {
        var update = new EntityUpdate { Mode = "static" };
        Assert.Equal("static", update.Mode);
        Assert.Equal("Resource", update.Name);
        Assert.False(update.SafeMode);
        Assert.Equal(string.Empty, update.Command);
    }

    // --- NirvanaAccountManager: 涅槃账号掩码（t6 已移植，t17 校验补齐） ---

    [Theory]
    [InlineData(null, "*")]
    [InlineData("", "*")]
    [InlineData("123", "*")]
    [InlineData("1234", "123*")]
    [InlineData("12345", "1234*")]
    [InlineData("123456", "123***")]
    [InlineData("123456789", "123***789")]
    // 说明：13 位以上取「前3 + **** + 后3」，参考源注释里的示例串长度与代码不一致，此处以代码为准。
    [InlineData("1234567890123", "123****123")]
    public void NirvanaAccountManager_MaskAccount_MatchesReference(string? account, string expected)
    {
        var method = typeof(NirvanaAccountManager).GetMethod(
            "MaskAccount",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        Assert.Equal(expected, method!.Invoke(null, [account]));
    }

    // --- NEL / 代理编排 ---

    [Fact]
    public void EntityNewName_HoldsIdAndName()
    {
        var entity = new EntityNewName("123", "Steve");
        Assert.Equal("123", entity.Id);
        Assert.Equal("Steve", entity.Name);

        var empty = new EntityNewName(null, null);
        Assert.Null(empty.Id);
        Assert.Null(empty.Name);
    }

    [Fact]
    public void HeypixelProtocol_RegistersPacketsIntoBasePackets()
    {
        // 触发 HeypixelProtocol 静态构造（首次访问即完成封包注册）
        HeypixelProtocol.Init();

        Assert.Equal("4661334467366178884", HeypixelProtocol.GameId);
        Assert.Contains(C2SConfigPluginMessage.RegisterPacket, PacketManager.BasePackets.Values);
        Assert.Contains(Lanw.Heypixel.Play.SaClientboundSetPlayerTeamPacket.RegisterPacket, PacketManager.BasePackets.Values);
    }

    [Fact]
    public void InterceptorManager_InitializesHeypixelAndExposesInterceptor()
    {
        // InterceptorManager 的静态构造负责 HeypixelProtocol.Init()
        RuntimeHelpers.RunClassConstructor(typeof(InterceptorManager).TypeHandle);

        Assert.NotNull(typeof(InterceptorManager).GetField("Interceptor"));
        Assert.Equal(2, typeof(InterceptorManager).GetConstructors().Length);
        Assert.Contains(C2SConfigPluginMessage.RegisterPacket, PacketManager.BasePackets.Values);
    }

    [Fact]
    public void ActiveGameAndProxies_GetIndex_StartsAtOneWhenEmpty()
    {
        Assert.Equal(1, ActiveGameAndProxies.GetIndex());
        Assert.Empty(ActiveGameAndProxies.GetAllProxies());
    }
}
