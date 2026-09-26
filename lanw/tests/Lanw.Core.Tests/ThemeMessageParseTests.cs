using Lanw.Public.Message;
using Xunit;

namespace Lanw.Core.Tests;

/// <summary>
/// ThemeMessage.ParseThemeValue 单元测试（对应原 Vue Home.vue 把 .fant.json 提交为 EntityValue，
/// 服务端按 "value" 取值）。这些用例锁定 .fant.json 的取值契约，防止主题导入静默失效。
/// </summary>
public sealed class ThemeMessageParseTests
{
    [Theory]
    [InlineData("{\"value\":\"nirvana\"}", "nirvana")]
    [InlineData("{\"Value\":\"ABC\"}", "ABC")]
    [InlineData("{ \"other\": 1, \"value\": \"x\" }", "x")]
    [InlineData("{\"value\":\" default \"}", " default ")]
    public void ParseThemeValue_读取_value_字段(string json, string expected)
    {
        Assert.Equal(expected, ThemeMessage.ParseThemeValue(json));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("{}")]
    [InlineData("[1,2,3]")]
    [InlineData("\"nirvana\"")]
    [InlineData("{\"name\":\"nirvana\"}")]
    [InlineData("{\"value\":\"\"}")]
    [InlineData("{\"value\":\"   \"}")]
    [InlineData("{\"value\":123}")]
    public void ParseThemeValue_缺字段或非法时返回_null(string? json)
    {
        Assert.Null(ThemeMessage.ParseThemeValue(json));
    }

    [Fact]
    public void ThemeMessage_配置键与源一致()
    {
        Assert.Equal("theme", ThemeMessage.ThemeKey);
        Assert.Equal("themeValue", ThemeMessage.ThemeValueKey);
    }
}
