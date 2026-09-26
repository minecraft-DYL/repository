using Lanw.Core;
using Lanw.Core.Entities.Nirvana;
using Lanw.Core.Utils.CodeTools;
using Lanw.Public.Entities.Nirvana;
using Lanw.WPFLauncher.Http;

namespace Lanw.Public.Manager;

/// <summary>
/// 涅槃云账号管理（由 Nirvana.Public.Manager.NirvanaAccountManager 移植，自研）。
/// 负责涅槃云登录、账号/天数信息查询与聊天开关。
/// </summary>
public static class NirvanaAccountManager
{
    private static double? _days;

    // 登录账号
    public static async Task Login(string account, string password)
    {
        // 账号/密码做 URL 编码：含中文或 & = + 空格 等字符时不再拼坏查询串（否则服务端会返回非预期响应）。
        var entity = await X19Extensions.Nirvana.ApiAsync<EntityNirvanaLogin>(
            "/api/login?mode=fantnel&account=" + Uri.EscapeDataString(account) + "&password=" + Uri.EscapeDataString(password));
        if (entity == null)
        {
            throw new ErrorCodeException(ErrorCode.LoginError);
        }

        if (string.IsNullOrEmpty(entity.Token))
        {
            // 业务失败：优先回传服务端中文 msg（如「密码长度不符合要求」「账号不存在」），无文案时用通用登录错误。
            throw new Exception(string.IsNullOrWhiteSpace(entity.Message)
                ? Code.GetMessage(ErrorCode.LoginError)
                : entity.Message);
        }

        _days = null;
        LanwConfig.SetValue("account", account);
        LanwConfig.SetValue("token", entity.Token);
    }

    // 获取信息
    public static EntityAccountNirvanaConfig GetLoginInfo()
    {
        return GetLoginInfoAsync().GetAwaiter().GetResult();
    }

    // 获取信息
    private static async Task<EntityAccountNirvanaConfig> GetLoginInfoAsync()
    {
        LanwConfig.IsLogin(); // 检查是否登录

        if (_days == null)
        {
            var entity = await X19Extensions.Nirvana.ApiAsync<EntityNirvanaInfo>("/api/info?mode=fantnel&" + LanwConfig.GetLoginT());
            if (entity == null)
            {
                throw new ErrorCodeException(ErrorCode.DetailError);
            }

            if (entity.Code == 22)
            {
                LanwConfig.Logout();
                throw new ErrorCodeException(ErrorCode.OnlineStatusExpired);
            }

            if (entity.Code is not null and not 1)
            {
                throw new Exception(string.IsNullOrWhiteSpace(entity.Message)
                    ? Code.GetMessage(ErrorCode.DetailError)
                    : entity.Message);
            }

            _days = entity.Days;
        }

        var config = new EntityAccountNirvanaConfig
        {
            Account = LanwConfig.GetValue<string>("account"),
            Days = _days.Value,
            HideAccount = LanwConfig.GetValue<bool>("hideAccount")
        };

        if (config.HideAccount)
        {
            config.Account = MaskAccount(config.Account);
        }

        return config;
    }

    private static string MaskAccount(string account)
    {
        if (string.IsNullOrEmpty(account))
        {
            return "*";
        }

        var length = account.Length;
        return length switch
        {
            // 例如: 1234567890123 -> 123****890123
            >= 13 => $"{account[..3]}****{account[(length - 3)..]}",
            // 例如: 123456789 -> 123***789
            >= 9 => $"{account[..3]}***{account[(length - 3)..]}",
            // 例如: 123456 -> 123***
            >= 6 => $"{account[..3]}***",
            // 例如: 12345 -> 1234*
            >= 5 => $"{account[..4]}*",
            // 例如: 1234 -> 123*
            >= 4 => $"{account[..3]}*",
            _ => "*"
        };
    }

    public static void SetChatEnable(string? value)
    {
        LanwConfig.SetValue("chatEnable", value);
    }
}