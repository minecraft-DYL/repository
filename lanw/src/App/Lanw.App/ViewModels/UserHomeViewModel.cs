using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lanw.Core;
using Lanw.Core.Entities.Nirvana;
using Lanw.Core.Utils.CodeTools;
using Lanw.Public.Manager;

namespace Lanw.App.ViewModels;

/// <summary>
/// 用户中心页视图模型（对应原 Vue nirvana/UserHome.vue + /api/nirvana/account/get）。
/// 数据链路：进程内直调 NirvanaAccountManager.GetLoginInfo()（t6/t17 移植）取涅槃账号信息
/// （剩余天数 / 掩码账号 / 隐藏账号开关），隐藏开关写 LanwConfig.hideAccount，退出登录走 LanwConfig.Logout()，无 HTTP。
/// </summary>
public sealed partial class UserHomeViewModel : ObservableObject
{
    /// <summary>载入过程中抑制「隐藏账号」开关回写配置。</summary>
    private bool _loading;

    /// <summary>账号展示（隐藏账号开关打开时为掩码形式）。</summary>
    [ObservableProperty]
    public partial string AccountDisplay { get; set; }

    /// <summary>剩余天数展示（对应原版“当前剩余 N 天。”）。</summary>
    [ObservableProperty]
    public partial string DaysText { get; set; }

    /// <summary>隐藏账号开关（对应 /api/nirvana/set?mode=hideAccount）。</summary>
    [ObservableProperty]
    public partial bool HideAccount { get; set; }

    /// <summary>是否已登录涅槃账号。</summary>
    [ObservableProperty]
    public partial bool IsLoggedIn { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReloadCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    public partial bool IsBusy { get; set; }

    /// <summary>涅槃云登录账号（对应原 Vue NirvanaLogin.vue 的 account）。</summary>
    [ObservableProperty]
    public partial string Account { get; set; }

    /// <summary>涅槃云登录密码（对应原 Vue NirvanaLogin.vue 的 password）。</summary>
    [ObservableProperty]
    public partial string Password { get; set; }

    [ObservableProperty]
    public partial Visibility CardVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility EmptyVisibility { get; set; }

    public UserHomeViewModel()
    {
        AccountDisplay = "未登录";
        DaysText = "当前剩余 0 天。";
        HideAccount = false;
        IsLoggedIn = false;
        StatusMessage = "正在读取涅槃账号信息…";
        IsBusy = false;
        Account = string.Empty;
        Password = string.Empty;
        CardVisibility = Visibility.Collapsed;
        EmptyVisibility = Visibility.Visible;
    }

    /// <summary>载入账号信息（进入页面时调用）。</summary>
    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        _loading = true;
        try
        {
            // GetLoginInfo 内部会先 IsLogin() 校验本地登录态，未登录抛 ErrorCodeException(LogInNot)
            var info = await Task.Run(() => NirvanaAccountManager.GetLoginInfo()).ConfigureAwait(true);
            Apply(info);
            StatusMessage = "涅槃账号信息已更新";
        }
        catch (ErrorCodeException e) when (e.Entity.Code == (int)ErrorCode.LogInNot)
        {
            NotLoggedIn("尚未登录涅槃账号，请在本页输入账号密码后登录。");
        }
        catch (Exception e)
        {
            // 本地已登录但取信息失败（服务不可用/网络异常）：退化为展示本地账号
            var account = TryGetLocalAccount();
            if (string.IsNullOrEmpty(account))
            {
                NotLoggedIn("获取涅槃账号信息失败：" + e.Message);
            }
            else
            {
                IsLoggedIn = true;
                HideAccount = TryGetHideAccount();
                AccountDisplay = account!;
                DaysText = "当前剩余天数：获取失败";
                CardVisibility = Visibility.Visible;
                EmptyVisibility = Visibility.Collapsed;
                StatusMessage = "获取涅槃账号信息失败：" + e.Message;
            }
        }
        finally
        {
            _loading = false;
            IsBusy = false;
            if (!IsLoggedIn)
            {
                CardVisibility = Visibility.Collapsed;
                EmptyVisibility = Visibility.Visible;
            }
        }
    }

    /// <summary>重新读取（对应参考页面 onMounted 的 loadAccountInfo）。</summary>
    [RelayCommand(CanExecute = nameof(CanReload))]
    private async Task ReloadAsync()
    {
        await LoadAsync();
    }

    private bool CanReload() => !IsBusy;

    private bool CanLogin() => !IsBusy;

    /// <summary>
    /// 涅槃云登录（对应原 Vue nirvana/NirvanaLogin.vue 与 nirvana/NirvanaLoginSocket.vue 的 toLogin）：
    /// loginNirvana(account, password) → getNirvanaAccount() → 剩余天数 days &gt; 0 才算登录成功，
    /// 否则提示「账号没有天数，无法登录」。账号与 token 由 NirvanaAccountManager.Login 写入 LanwConfig。
    /// 登录成功后本页直接展示账号信息卡（原版登录成功后跳转 /user，即本页）。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Account))
        {
            StatusMessage = "请输入账号";
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            StatusMessage = "请输入密码";
            return;
        }

        IsBusy = true;
        StatusMessage = "正在登录涅槃账号…";
        try
        {
            var account = Account.Trim();
            var password = Password;
            await Task.Run(() => NirvanaAccountManager.Login(account, password)).ConfigureAwait(true);

            var info = await Task.Run(() => NirvanaAccountManager.GetLoginInfo()).ConfigureAwait(true);
            if (info.Days > 0)
            {
                Apply(info);
                Password = string.Empty;
                StatusMessage = $"登录成功：{info.Account}";
            }
            else
            {
                NotLoggedIn("账号没有天数，无法登录");
            }
        }
        catch (ErrorCodeException e)
        {
            var raw = e.Entity.Code ?? (int)ErrorCode.Failure;
            StatusMessage = "登录失败：" + Code.GetMessage((ErrorCode)raw);
        }
        catch (Exception e)
        {
            StatusMessage = "登录失败：" + (string.IsNullOrWhiteSpace(e.Message) ? "请检查网络连接" : e.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>退出登录（对应 /api/nirvana/logout → NirvanaConfig.Logout()）。</summary>
    public void Logout()
    {
        try
        {
            LanwConfig.Logout();
            NotLoggedIn("已退出涅槃账号登录");
        }
        catch (Exception e)
        {
            StatusMessage = "退出登录失败：" + e.Message;
        }
    }

    /// <summary>隐藏账号开关变化（对应 Vue 的 watch(hideAccount) → hideNirvanaAccount）。</summary>
    partial void OnHideAccountChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        try
        {
            LanwConfig.SetValue("hideAccount", value);
            StatusMessage = value ? "已开启隐藏账号（重新读取后生效）" : "已关闭隐藏账号";
            _ = LoadAsync();
        }
        catch (Exception e)
        {
            StatusMessage = "设置隐藏账号失败：" + e.Message;
        }
    }

    private void Apply(EntityAccountNirvanaConfig info)
    {
        IsLoggedIn = true;
        HideAccount = info.HideAccount;
        AccountDisplay = string.IsNullOrEmpty(info.Account) ? "（账号为空）" : info.Account;
        DaysText = $"当前剩余 {info.Days:0} 天。";
        CardVisibility = Visibility.Visible;
        EmptyVisibility = Visibility.Collapsed;
    }

    private void NotLoggedIn(string message)
    {
        IsLoggedIn = false;
        AccountDisplay = "未登录";
        DaysText = "当前剩余 0 天。";
        CardVisibility = Visibility.Collapsed;
        EmptyVisibility = Visibility.Visible;
        StatusMessage = message;
    }

    private static string? TryGetLocalAccount()
    {
        try
        {
            var account = LanwConfig.GetValue<string>("account");
            return string.IsNullOrEmpty(account) ? null : account;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool TryGetHideAccount()
    {
        try
        {
            return LanwConfig.GetValue<bool>("hideAccount");
        }
        catch (Exception)
        {
            return false;
        }
    }
}
