using Lanw.Core;

namespace Lanw.Chat.Utils;

/// <summary>
///     聊天室用到的全局配置读写（对应参考源 NirvanaAPI.NirvanaConfig 的 GetString / GetBool / IsLogin / Logout）。
///
///     lanw 中全局配置由 <see cref="LanwConfig" /> 承载；本适配层只做「缺省值不落盘、未配置返回空」的兼容，
///     避免读取未写入过的 account/token 时抛异常或触发配置落盘。
/// </summary>
public static class ChatConfig {
    /// <summary>读字符串配置，未配置时返回空串。</summary>
    public static string GetString(string name)
    {
        try {
            return LanwConfig.GetValue<string>(name) ?? string.Empty;
        } catch (Exception) {
            return string.Empty;
        }
    }

    /// <summary>读布尔配置，未配置时返回 false。</summary>
    public static bool GetBool(string name)
    {
        try {
            return LanwConfig.GetValue<bool>(name);
        } catch (Exception) {
            return false;
        }
    }

    /// <summary>登录检测，未登录抛 ErrorCodeException。</summary>
    public static void IsLogin()
    {
        LanwConfig.IsLogin();
    }

    /// <summary>退出登录（清空 account/token）。</summary>
    public static void Logout()
    {
        LanwConfig.Logout();
    }
}
