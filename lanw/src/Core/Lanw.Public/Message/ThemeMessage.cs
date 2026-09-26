using System.Text.Json;
using Lanw.Core.Entities;
using Lanw.Core.Utils;
using Lanw.Public.Entities.Update;
using Lanw.WPFLauncher.Http;

namespace Lanw.Public.Message;

/// <summary>
/// 主题编排（内嵌等价：Fantnel.Servlet OthersController 的 GET /api/theme、GET /api/theme/set、
/// POST /api/theme/switch，以及 Nirvana.Public InitProgram.SafeTheme）。
/// 桌面版无本地 HTTP，主页「主题包导入」直接调用本类。
/// </summary>
public static class ThemeMessage
{
    /// <summary>当前主题名配置键（对应 ConfigUtil.SaveConfig("theme", name)）。</summary>
    public const string ThemeKey = "theme";

    /// <summary>当前生效主题值配置键（对应 ConfigUtil.SaveConfig("themeValue", value)）。</summary>
    public const string ThemeValueKey = "themeValue";

    /// <summary>对应 GET /api/theme：读取当前主题名（缺省 default）。</summary>
    public static string GetTheme() => ConfigUtil.GetConfig(ThemeKey, "default");

    /// <summary>对应 GET /api/theme/set?name=：写入当前主题名。</summary>
    public static void SetTheme(string name) => ConfigUtil.SaveConfig(ThemeKey, name);

    /// <summary>对应 InitProgram.SafeTheme：向涅槃服务器校验主题名是否有效。</summary>
    public static async Task<bool> SafeThemeAsync(string themeValue)
        => await X19Extensions.Nirvana.ApiAsync<EntityResponseBase>("/api/theme/name?value=" + themeValue).ConfigureAwait(false) is { Code: 1 };

    /// <summary>
    /// 对应 POST /api/theme/switch：值为空视为参数错误（原版返回 ParamError，不生效）；
    /// SafeTheme 校验通过才写入 themeValue，随后按 ui.&lt;主题名&gt; 触发主题文件更新。
    /// 原版即使 SafeTheme 未通过也会执行一次更新并返回 Success，此处保持一致。
    /// </summary>
    public static async Task SwitchThemeAsync(string? themeValue)
    {
        if (string.IsNullOrEmpty(themeValue))
        {
            return;
        }

        if (await SafeThemeAsync(themeValue).ConfigureAwait(false))
        {
            ConfigUtil.SaveConfig(ThemeValueKey, themeValue);
        }

        new EntityUpdate
        {
            Mode = "ui." + themeValue,
            Name = "Fantnel UI"
        }.CheckUpdateSafe().GetAwaiter().GetResult();
    }

    /// <summary>
    /// 解析主题包文件（.fant.json）里的主题名：原版主页把整个 json 作为请求体提交，
    /// 服务端按 EntityValue（JsonPropertyName "value"）取值，故此处等价取 "value"（大小写不敏感）。
    /// 解析失败 / 缺字段 / 空值返回 null（前端对应提示「Fantnel 主题 文件解析失败，请检查文件格式。」）。
    /// </summary>
    public static string? ParseThemeValue(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, "value", StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String)
                {
                    var value = property.Value.GetString();
                    return string.IsNullOrWhiteSpace(value) ? null : value;
                }
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
