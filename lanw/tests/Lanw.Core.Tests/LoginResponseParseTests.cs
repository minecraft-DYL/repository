using System.Text.Json;
using Lanw.Core.Entities;
using Lanw.Core.Utils;
using Lanw.Public.Entities.Nirvana;
using Lanw.WPFLauncher.Entities;
using Lanw.WPFLauncher.Http;

namespace Lanw.Core.Tests;

/// <summary>
/// 登录响应解析健壮性测试：锁定「服务端错误信封 / 非 JSON 响应」两类真实故障不再把原始
/// JsonException（$.data / '0xE8' is an invalid start of a value）冒泡到 UI。
/// 用例字符串取自对 http://110.42.70.32:13423 的真实抓包。
/// </summary>
public sealed class LoginResponseParseTests
{
    // 真实抓包：/api/fantnel/captcha 识别失败时 data 是对象，成功时是字符串。
    private const string CaptchaOk = "{\"code\":1,\"msg\":\"成功\",\"message\":\"成功\",\"data\":\"qnen9\"}";
    private const string CaptchaObjectData = "{\"code\":51,\"msg\":\"{\\\"code\\\":0,\\\"result\\\":\\\"qn\\\"}\\n\",\"data\":{\"message\":\"{\\\"code\\\":0,\\\"result\\\":\\\"qn\\\"}\\n\"}}";

    [Fact]
    public void CaptchaResponse_成功时取到识别文本()
    {
        var entity = JsonSerializer.Deserialize<EntityCaptchaResponse>(CaptchaOk);
        Assert.NotNull(entity);
        Assert.Equal(1, entity!.Code);
        Assert.Equal("qnen9", entity.Data);
    }

    [Fact]
    public void CaptchaResponse_data是对象时不再抛_json_异常()
    {
        var entity = JsonSerializer.Deserialize<EntityCaptchaResponse>(CaptchaObjectData);
        Assert.NotNull(entity);
        Assert.Equal(51, entity!.Code);
        // 对象里的 message 被宽松取为字符串（旧实现会在此抛 $.data 转换异常）
        Assert.Contains("result", entity.Data);
    }

    [Fact]
    public void CaptchaResponse_data是数组时取首个字符串()
    {
        var entity = JsonSerializer.Deserialize<EntityCaptchaResponse>("{\"code\":7,\"data\":[\"abc\",\"def\"]}");
        Assert.NotNull(entity);
        Assert.Equal("abc", entity!.Data);
    }

    // 真实抓包：/api/info 错误信封不含 days；旧 required days 会在解析期先炸掉。
    [Fact]
    public void NirvanaInfo_错误信封缺_days_时可解析并读到_code()
    {
        const string json = "{\"code\":12,\"msg\":[\"账号不存在\",[{\"methodName\":\"toException\"}]]}";
        var entity = JsonSerializer.Deserialize<EntityNirvanaInfo>(json);
        Assert.NotNull(entity);
        Assert.Equal(12, entity!.Code);
        Assert.Equal(0d, entity.Days);
        Assert.Equal("账号不存在", entity.Message);
    }

    [Fact]
    public void NirvanaLogin_msg是数组时取首个字符串()
    {
        const string json = "{\"code\":15,\"msg\":[\"密码长度不符合要求\",[{\"methodName\":\"toException\"}]]}";
        var entity = JsonSerializer.Deserialize<EntityNirvanaLogin>(json);
        Assert.NotNull(entity);
        Assert.Equal(15, entity!.Code);
        Assert.Null(entity.Token);
        Assert.Equal("密码长度不符合要求", entity.Message);
    }

    [Fact]
    public void SafeJson_非json_抛出带中文说明与预览的异常()
    {
        var ex = Assert.Throws<JsonException>(() => SafeJson.Deserialize<EntityResponseBase>("登录失败，请重试", "4399com 登录响应"));
        Assert.Contains("4399com 登录响应", ex.Message);
        Assert.Contains("登录失败", ex.Message);
    }

    [Fact]
    public void SafeJson_空响应_抛出带中文说明的异常()
    {
        var ex = Assert.Throws<JsonException>(() => SafeJson.Deserialize<EntityResponseBase>("  ", "接口X"));
        Assert.Contains("接口X", ex.Message);
    }

    // 走真实 HTTP 解析收口：非 JSON 文本不再泄漏 '0xE8' is an invalid start of a value。
    [Fact]
    public void X19_解析非json响应_转为中文可读异常()
    {
        var ex = Assert.Throws<EntityX19Exception>(() => X19Extensions.ToType<EntityResponseBase>("\u8d26\u53f7\u5f02\u5e38", "/api/login"));
        Assert.Contains("/api/login", ex.Message);
        Assert.DoesNotContain("invalid start of a value", ex.Message);
    }

    [Fact]
    public void X19_解析合法json_正常返回()
    {
        var entity = X19Extensions.ToType<EntityResponseBase>("{\"code\":1,\"msg\":\"成功\"}", "/api/tick");
        Assert.NotNull(entity);
        Assert.Equal(1, entity!.Code);
        Assert.Equal("成功", entity.Message);
    }

    [Fact]
    public void X19_字符串请求原始文本_不做解析()
    {
        var raw = X19Extensions.ToType<string>("not json at all", "/pl/x19_java_patchlist");
        Assert.Equal("not json at all", raw);
    }
}
