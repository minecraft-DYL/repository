namespace Lanw.App.Models;

/// <summary>
/// 登录类型选项（对应实体账号的 Type 字段，与 Fantnel 一致）。
/// </summary>
public sealed record LoginTypeOption(string Key, string DisplayName)
{
    /// <summary>
    /// 默认登录类型集合：cookie / 4399 / 4399com / 163Email。
    /// </summary>
    public static IEnumerable<LoginTypeOption> GetDefaults()
    {
        yield return new LoginTypeOption("cookie", "Cookie 登录");
        yield return new LoginTypeOption("4399", "4399 账号登录");
        yield return new LoginTypeOption("4399com", "4399com 账号登录");
        yield return new LoginTypeOption("163Email", "网易邮箱登录");
    }

    public override string ToString() => DisplayName;
}