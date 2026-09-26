using Lanw.Core;
using Lanw.Core.Entities;
using Lanw.Core.Entities.Login;
using Lanw.Core.Utils;
using Lanw.Core.Utils.CodeTools;
using Lanw.Core.Utils.Progress;

namespace Lanw.Core.Tests;

// 校验 Nirvana.Common → Lanw.Core 移植后的行为（逻辑等价，自研命名）。
public class CommonPortTests
{
    // --- ErrorCode / Code / ErrorCodeException ---
    [Fact]
    public void ErrorCodeException_ShouldCarryMessage_AndEntity()
    {
        var ex = new ErrorCodeException(ErrorCode.PasswordError);
        Assert.Equal("密码错误或异常", ex.Message);
        Assert.NotNull(ex.Entity);
        Assert.Equal((int)ErrorCode.PasswordError, ex.Entity.Code);
        Assert.Equal("密码错误或异常", ex.Entity.Message);
        Assert.Same(ex.GetJson(), ex.Entity);
    }

    [Fact]
    public void Code_GetMessage_ShouldReturnChineseMessage()
    {
        Assert.Equal("成功", Code.GetMessage(ErrorCode.Success));
        Assert.Equal("没有登录", Code.GetMessage(ErrorCode.LogInNot));
        Assert.Equal("未知错误", Code.GetMessage((ErrorCode)999));
    }

    [Theory]
    [InlineData(ErrorCode.Failure, 0)]
    [InlineData(ErrorCode.Success, 1)]
    [InlineData(ErrorCode.LogInNot, 15)]
    [InlineData(ErrorCode.NotVersionByLauncher, 33)]
    public void ErrorCode_Values_ShouldMatchFantnel(ErrorCode code, int expected)
    {
        Assert.Equal(expected, (int)code);
    }

    // --- EntityUserInfo / EntityAccount ---
    [Fact]
    public void EntityUserInfo_GetToken_ShouldThrowWhenEmpty()
    {
        var info = new EntityUserInfo();
        Assert.False(info.IsNotNuLl());
        Assert.Throws<ErrorCodeException>(() => info.GetToken());
        Assert.Throws<ErrorCodeException>(() => info.GetUserId());
    }

    [Fact]
    public void EntityUserInfo_IsNotNuLl_ShouldRequireBoth()
    {
        Assert.False(new EntityUserInfo { Token = "t" }.IsNotNuLl());
        Assert.False(new EntityUserInfo { UserId = "u" }.IsNotNuLl());
        Assert.True(new EntityUserInfo { Token = "t", UserId = "u" }.IsNotNuLl());
    }

    [Fact]
    public void EntityAccount_CookieEquals_ByPassword()
    {
        var a = new EntityAccount { Type = "cookie", Password = "abc" };
        var b = new EntityAccount { Type = "cookie", Password = "abc" };
        Assert.True(a.Equals(b));
        Assert.False(a.Equals(new EntityAccount { Type = "cookie", Password = "xyz" }));
    }

    [Fact]
    public void EntityAccount_AccountEquals_ByAccountTypePassword()
    {
        var a = new EntityAccount { Type = "4399", Account = "u", Password = "p" };
        var b = new EntityAccount { Type = "4399", Account = "u", Password = "p" };
        Assert.True(a.Equals(b));
        Assert.False(a.Equals(new EntityAccount { Type = "4399com", Account = "u", Password = "p" }));
    }

    // --- ConfigValue ---
    [Fact]
    public void ConfigValue_Default_And_IsDefault()
    {
        var cfg = new ConfigValue<bool>(true) { Name = "x" };
        Assert.True(cfg.IsDefault());
        Assert.True((bool?)cfg.GetValueTo());
        cfg.SetFrom("false");
        Assert.False(cfg.IsDefault());
        Assert.False((bool?)cfg.GetValueTo());
    }

    [Fact]
    public void ConfigValue_StringToNumber_Conversion()
    {
        var cfg = new ConfigValue<double>(1.0) { Name = "mem" };
        cfg.SetFrom("4096");
        Assert.Equal(4096.0, (double)cfg.GetValueTo()!);
    }

    // --- LanwConfig ---
    [Fact]
    public void LanwConfig_GetValue_WithDefault()
    {
        // gameMemory 默认 4096
        Assert.Equal(4096, LanwConfig.GetValue<int>("gameMemory"));
        Assert.True(LanwConfig.GetValue<bool>("autoLoginGame"));
    }

    [Fact]
    public void LanwConfig_SetValue_And_GetValue_RoundTrip()
    {
        var original = LanwConfig.GetValue<int>("gameMemory");
        try {
            LanwConfig.SetValue("gameMemory", 2048, save: false);
            Assert.Equal(2048, LanwConfig.GetValue<int>("gameMemory"));
        } finally {
            LanwConfig.SetValue("gameMemory", original, save: false);
        }
    }

    [Fact]
    public void LanwConfig_SetGameMemory_RejectsBelow1024()
    {
        Assert.Throws<ErrorCodeException>(() => LanwConfig.SetGameMemory("512"));
        Assert.Throws<ErrorCodeException>(() => LanwConfig.SetGameMemory(null));
    }

    // --- Tools / FileUtil / PathUtil ---
    [Fact]
    public void Tools_GetBetweenStrings()
    {
        Assert.Equal("42", Tools.GetBetweenStrings("a=42&b=1", "a=", "&"));
        Assert.Equal(string.Empty, Tools.GetBetweenStrings("abc", "x", "y"));
    }

    [Fact]
    public void Tools_DetectOperatingSystem_And_Architecture()
    {
        Assert.Contains(Tools.DetectOperatingSystemMode(), new[] { "win", "linux", "mac" });
        Assert.Contains(Tools.DetectArchitectureMode(), new[] { "x64", "arm64" });
    }

    [Fact]
    public void Tools_GetMessage_ForErrorCodeException()
    {
        var ex = new ErrorCodeException(ErrorCode.AccountError);
        Assert.Equal("账号错误或异常", Tools.GetMessage(ex));
    }

    [Fact]
    public void FileUtil_ComputeMd5_ReturnsUpperHex_OrEmpty()
    {
        Assert.Equal(string.Empty, FileUtil.ComputeMd5FromFile(string.Empty));
        Assert.Equal(string.Empty, FileUtil.ComputeMd5FromFile(@"Z:\__nonexistent__\x"));

        var tmp = Path.GetTempFileName();
        try {
            File.WriteAllText(tmp, "lanw");
            var md5 = FileUtil.ComputeMd5FromFile(tmp);
            Assert.Equal(32, md5.Length);
            Assert.Equal(md5.ToUpperInvariant(), md5);
        } finally {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void PathUtil_ConfigPath_UnderResourcePath()
    {
        Assert.EndsWith("nirvanaAccount.json", PathUtil.ConfigPath);
        Assert.StartsWith(PathUtil.ResourcePath, PathUtil.ConfigPath);
    }

    // --- FirstStringConverter：msg 可以是数组，取首元素 ---
    [Fact]
    public void EntityResponse_Message_FromArray_TakesFirstString()
    {
        const string json = """{"code":1,"msg":["hello","world"],"data":null}""";
        var resp = System.Text.Json.JsonSerializer.Deserialize<EntityResponse<object>>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(resp);
        Assert.Equal(1, resp.Code);
        Assert.Equal("hello", resp.Message);
    }

    // --- Progress ---
    [Fact]
    public void SyncCallback_ShouldRaiseHandler()
    {
        double? seen = null;
        var cb = new SyncCallback<SyncProgressBarUtil.ProgressReport>(r => seen = r.Percent);
        cb.Report(new SyncProgressBarUtil.ProgressReport { Percent = 66.6 });
        Assert.Equal(66.6, seen);
    }
}