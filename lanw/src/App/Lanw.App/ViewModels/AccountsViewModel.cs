using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lanw.App.Models;
using Lanw.App.Services;
using Lanw.Core.Entities.Login;
using Lanw.Core.Utils.CodeTools;
using Lanw.Public.Entities.Nirvana;
using Lanw.Public.Message;

namespace Lanw.App.ViewModels;

/// <summary>
/// 账号管理页视图模型：账号列表展示、新增账号（登录并保存）、删除、设为当前、切换账号、注销、自动登录、
/// 随机登录（Geetest 人机验证 + 随机 4399 账号）与当前登录态。
/// 列表增删来自进程内持久化（AccountRepository / account.json）；登录/切换/注销/自动登录/随机登录接线到
/// Lanw.Public.AccountMessage 与 Lanw.Core.InfoManager（经 <see cref="AuthService"/>）。
/// 视图负责弹窗（凭据输入、4399 图片验证码、Geetest 网页验证），通过
/// <see cref="CaptchaPrompt"/> / <see cref="GeetestPrompt"/> 回调注入。
/// </summary>
public sealed partial class AccountsViewModel : ObservableObject
{
    private readonly AuthService _auth;

    public AccountRepository Repository { get; }

    /// <summary>可选登录类型（对应实体账号 Type）。</summary>
    public IReadOnlyList<LoginTypeOption> LoginTypes => LoginTypeOption.GetDefaults().ToList();

    /// <summary>账号列表（进程内持久化只读展示）。</summary>
    public ObservableCollection<AccountItemViewModel> Accounts { get; } = [];

    /// <summary>视图提供的 4399 图片验证码询问回调（返回用户输入，取消返回 null）。由页面注入。</summary>
    public Func<Task<string?>>? CaptchaPrompt { get; set; }

    /// <summary>视图提供的账号池网页端回调（关闭页面返回 null）。由页面注入。</summary>
    public Func<Task<EntityGeeTest?>>? GeetestPrompt { get; set; }

    /// <summary>视图提供的「编辑账号」弹窗回调（对应原版 GameAccounts.vue 的 editAccount → 编辑弹窗）。由页面注入。</summary>
    public Func<AccountItemViewModel, Task>? EditPrompt { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    [ObservableProperty]
    public partial string CurrentDisplay { get; set; }

    /// <summary>是否处于已登录态。</summary>
    [ObservableProperty]
    public partial bool IsLoggedIn { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>登录态徽标文本（"已登录"/"未登录"）。</summary>
    [ObservableProperty]
    public partial string LoginStateText { get; set; }

    /// <summary>登录态徽标可见性。</summary>
    [ObservableProperty]
    public partial Visibility LoginStateBadgeVisibility { get; set; }

    /// <summary>空列表提示可见性（没有任何已保存账号时显示）。</summary>
    [ObservableProperty]
    public partial Visibility EmptyStateVisibility { get; set; }

    /// <summary>最近一次拉取到的 4399 验证码图片字节（供弹窗显示）。</summary>
    [ObservableProperty]
    public partial byte[]? CaptchaImage { get; set; }

    public AccountsViewModel(AccountRepository? repository = null)
    {
        Repository = repository ?? new AccountRepository();
        _auth = new AuthService(Repository);
        StatusMessage = string.Empty;
        CurrentDisplay = "未登录";

        RefreshList();
        UpdateLoginState();
    }

    /// <summary>从持久化存储重载账号列表，并刷新当前账号展示与登录态。</summary>
    [RelayCommand]
    private void RefreshList()
    {
        Accounts.Clear();
        foreach (var account in Repository.Load())
        {
            Accounts.Add(new AccountItemViewModel(this, account));
        }

        UpdateCurrentDisplay();
        UpdateLoginState();
        EmptyStateVisibility = Accounts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// 登录并保存（原「登录」页流程）：提交凭据 -> 协议登录（4399 / 4399com 走自动识别验证码）
    /// -> 自动识别失败时弹窗手动输入验证码并重试一次。
    /// 登录成功后协议会把账号写入 account.json，本方法随后刷新列表与登录态。
    /// 返回是否登录成功（视图据此决定是否关闭「添加账号」弹窗）。
    /// </summary>
    public async Task<bool> LoginWithCredentialsAsync(string? typeKey, string account, string password, string? name = null)
    {
        if (IsBusy)
        {
            StatusMessage = "正在处理上一个操作，请稍候…";
            return false;
        }

        if (string.IsNullOrEmpty(typeKey))
        {
            StatusMessage = "请选择登录类型";
            return false;
        }

        if (typeKey == "cookie")
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                StatusMessage = "Cookie 登录需填写 Cookie 凭据字符串";
                return false;
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(account))
            {
                StatusMessage = "请输入账号";
                return false;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                StatusMessage = "请输入密码";
                return false;
            }
        }

        var pending = typeKey == "cookie"
            ? new EntityAccount { Type = typeKey, Password = password }
            : new EntityAccount { Type = typeKey, Account = account, Password = password };

        // 原版「名称」字段（列表展示名）：留空则沿用协议写入的默认值。
        if (!string.IsNullOrWhiteSpace(name))
        {
            pending.Name = name.Trim();
        }

        IsBusy = true;
        StatusMessage = "登录中…";
        var success = false;
        try
        {
            EntityAccount? current;
            try
            {
                current = await _auth.LoginAsync(pending).ConfigureAwait(true);
            }
            catch (Exception first) when (NeedsManualCaptcha(typeKey, first))
            {
                // 4399 自动识别失败 / 无验证码会话：转为手动输入验证码。
                var captcha = await PromptCaptchaAsync().ConfigureAwait(true);
                if (string.IsNullOrWhiteSpace(captcha))
                {
                    StatusMessage = "已取消验证码输入，登录未完成";
                    return false;
                }

                StatusMessage = "已提交验证码，正在登录…";
                current = await _auth.LoginWithCaptchaAsync(pending, captcha).ConfigureAwait(true);
            }

            if (current is { Token: not null, UserId: not null })
            {
                StatusMessage = $"登录成功：{NameOf(current)}（已保存到账号管理）";
                success = true;
            }
            else
            {
                StatusMessage = "登录未返回有效凭证，请检查账号或验证码";
            }
        }
        catch (Exception e)
        {
            StatusMessage = "登录失败：" + DescribeError(e);
        }
        finally
        {
            IsBusy = false;
            RefreshList();
        }

        return success;
    }

    /// <summary>拉取 4399 验证码图片（供弹窗刷新按钮调用），同时更新 <see cref="CaptchaImage"/>。</summary>
    public async Task<byte[]?> LoadCaptchaAsync()
    {
        var bytes = await _auth.LoadCaptcha4399Async().ConfigureAwait(true);
        CaptchaImage = bytes;
        return bytes;
    }

    /// <summary>
    /// 随机登录（对应原版 GameAccounts.vue 的「随机登录」）：
    /// Geetest 网页人机验证 -> <see cref="AccountMessage.RandomAccount"/> 获取随机 4399 账号并自动登录（落盘）。
    /// 所有失败路径都有明确中文提示，绝不静默失败。
    /// </summary>
    [RelayCommand]
    private async Task RandomLoginAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (GeetestPrompt is null)
        {
            StatusMessage = "随机登录不可用：界面未就绪（无法打开账号池网页端）";
            return;
        }

        IsBusy = true;
        StatusMessage = "正在打开账号池网页端…";
        try
        {
            var captcha = await GeetestPrompt().ConfigureAwait(true);
            if (captcha is null)
            {
                StatusMessage = "已关闭账号池网页端；若已获取随机账号，可点「添加账号」把它加进列表。";
                return;
            }

            StatusMessage = "账号池网页端已返回，正在获取随机账号…";
            await Task.Run(() => AccountMessage.RandomAccount(captcha)).ConfigureAwait(true);
            StatusMessage = "随机登录成功：已获取随机 4399 账号并自动登录（已保存到账号列表）";
        }
        catch (Exception e)
        {
            StatusMessage = "随机登录失败：" + DescribeRandomLoginError(e);
        }
        finally
        {
            IsBusy = false;
            RefreshList();
        }
    }

    /// <summary>启动自动登录上次活跃账号。</summary>
    [RelayCommand]
    private async Task AutoLoginLastActiveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "正在自动登录上次活跃账号…";
        try
        {
            var current = await _auth.AutoLoginLastActiveAsync().ConfigureAwait(true);
            StatusMessage = current is { Token: not null, UserId: not null }
                ? $"自动登录成功：{NameOf(current)}"
                : "无可自动登录的账号或登录未完成";
        }
        catch (Exception e)
        {
            StatusMessage = "自动登录失败：" + MessageOf(e);
        }
        finally
        {
            IsBusy = false;
            RefreshList();
        }
    }

    /// <summary>删除账号（由行内命令调用）。</summary>
    public void DeleteAccount(AccountItemViewModel item)
    {
        Repository.Delete(item.Id);
        StatusMessage = $"已删除账号 #{item.Id}";
        RefreshList();
    }

    /// <summary>行内「编辑」入口：由视图弹窗（EditPrompt）完成，见 AccountsPage.ShowEditDialogAsync。</summary>
    public void RequestEditAccount(AccountItemViewModel item)
    {
        if (EditPrompt is null)
        {
            StatusMessage = "编辑不可用：界面未就绪";
            return;
        }

        _ = EditPrompt(item);
    }

    /// <summary>
    /// 账号表单校验（对齐原版 addNewAccount / saveAccount 的客户端校验与文案）：
    /// 名称与密码必填 →「请填写所有基本信息」；非 cookie 型账号字段必填 →「请填写所有必填字段」。
    /// 返回 null 表示通过。
    /// </summary>
    public static string? ValidateAccountForm(string? name, string? account, string? password, string? typeKey)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(password))
        {
            return "请填写所有基本信息";
        }

        if (typeKey != "cookie" && string.IsNullOrWhiteSpace(account))
        {
            return "请填写所有必填字段";
        }

        return null;
    }

    /// <summary>
    /// 保存账号编辑（对应原版 GameAccounts.vue 的 saveAccount → updateAccount）：
    /// 校验 → 写回该账号并持久化（AccountMessage.UpdateAccount，与原版同一条链）→ 刷新列表。
    /// 校验失败返回 false 并把原文案写入 error（视图据此保留弹窗显示错误）。
    /// </summary>
    public bool ApplyAccountEdit(AccountItemViewModel item, string name, string account, string password, string? typeKey, out string error)
    {
        error = ValidateAccountForm(name, account, password, typeKey) ?? string.Empty;
        if (error.Length > 0)
        {
            return false;
        }

        try
        {
            var source = item.Account;

            // Password 为 init-only：用对象初始化器重建实体（保留 Id 与登录态 Token/UserId），
            // 再走与原版同一条 UpdateAccount 持久化链。
            var updated = new EntityAccount
            {
                Id = source.Id,
                Name = name.Trim(),
                Type = typeKey,
                Password = password,
                Account = typeKey == "cookie" ? source.Account : account.Trim(),
                UserId = source.UserId,
                Token = source.Token,
            };

            AccountMessage.UpdateAccount(updated);
            StatusMessage = $"已保存账号：{updated.Name}";
            RefreshList();
            return true;
        }
        catch (Exception e)
        {
            error = "保存失败：" + MessageOf(e);
            StatusMessage = error;
            return false;
        }
    }

    /// <summary>设为当前账号（真实切换：AccountMessage.SwitchAccountToForce）。</summary>
    public async void SetCurrentAccount(AccountItemViewModel item)
    {
        IsBusy = true;
        StatusMessage = $"正在切换至 {item.DisplayName}…";
        try
        {
            await _auth.SwitchAsync(item.Id).ConfigureAwait(true);
            StatusMessage = $"已设为当前账号：{item.DisplayName}";
        }
        catch (Exception e)
        {
            StatusMessage = "切换失败：" + MessageOf(e);
        }
        finally
        {
            IsBusy = false;
            RefreshList();
        }
    }

    /// <summary>切换账号入口：切换并自动登录目标账号（已登录设当前，否则走协议）。</summary>
    public async void SwitchToAccount(AccountItemViewModel item)
    {
        IsBusy = true;
        StatusMessage = $"正在切换到 {item.DisplayName}…";
        try
        {
            var current = await _auth.SwitchAndLoginAsync(item.Account).ConfigureAwait(true);
            StatusMessage = current is { Token: not null, UserId: not null }
                ? $"已切换至账号：{item.DisplayName}"
                : $"已设当前账号：{item.DisplayName}（登录未完成）";
        }
        catch (Exception e)
        {
            StatusMessage = "切换失败：" + MessageOf(e);
        }
        finally
        {
            IsBusy = false;
            RefreshList();
        }
    }

    /// <summary>注销退出：仅清除当前登录态（内存态）。</summary>
    [RelayCommand]
    private void Logout()
    {
        _auth.Logout();
        StatusMessage = "已注销当前账号";
        RefreshList();
    }

    /// <summary>先拉取验证码图片，再交由视图弹窗询问用户输入。</summary>
    private async Task<string?> PromptCaptchaAsync()
    {
        await LoadCaptchaAsync().ConfigureAwait(true);
        return CaptchaPrompt is null ? null : await CaptchaPrompt().ConfigureAwait(true);
    }

    /// <summary>判断该异常是否属于"需要手动输入 4399 验证码"的情形。</summary>
    private static bool NeedsManualCaptcha(string type, Exception e)
    {
        if (type is not ("4399" or "4399com"))
        {
            return false;
        }

        var inner = Unwrap(e);
        return inner is ErrorCodeException error &&
               error.Entity.Code is int rawCode &&
               (ErrorCode)rawCode is ErrorCode.CaptchaNot or ErrorCode.CaptchaError;
    }

    private void UpdateCurrentDisplay()
    {
        var current = _auth.GetCurrent();
        CurrentDisplay = current switch
        {
            null => "未登录",
            _ => string.IsNullOrWhiteSpace(current.Name) ? (current.Account ?? "当前账号") : current.Name,
        };
    }

    private void UpdateLoginState()
    {
        IsLoggedIn = _auth.IsLoggedIn;
        LoginStateText = IsLoggedIn ? "已登录" : "未登录";
        LoginStateBadgeVisibility = IsLoggedIn ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>把异常整理成用户可读的中文提示（错误码优先使用协议文案，其次服务端原始 msg）。</summary>
    private static string DescribeError(Exception e)
    {
        var inner = Unwrap(e);
        if (inner is ErrorCodeException error)
        {
            var rawCode = error.Entity.Code ?? (int)ErrorCode.Failure;
            // 服务端信封里的中文 msg（通过 data 透传，如验证码识别失败原因）优先于本地错误码文案。
            var serverMessage = error.Entity.Data as string;
            var text = string.IsNullOrWhiteSpace(serverMessage)
                ? Code.GetMessage((ErrorCode)rawCode)
                : serverMessage;
            return $"{text}（错误码 {rawCode}）";
        }

        return string.IsNullOrWhiteSpace(inner.Message) ? "未知错误" : inner.Message;
    }

    /// <summary>随机登录失败提示：在通用文案上补充可操作的中文说明（未登录 / 次数用尽 / 登录态过期 / 网络不可用）。</summary>
    private static string DescribeRandomLoginError(Exception e)
    {
        var inner = Unwrap(e);
        if (inner is ErrorCodeException error)
        {
            var rawCode = error.Entity.Code ?? (int)ErrorCode.Failure;
            var message = Code.GetMessage((ErrorCode)rawCode);
            return (ErrorCode)rawCode switch
            {
                ErrorCode.LogInNot => $"请先在启动器中登录脱盒（Nirvana）账号后再试：{message}（错误码 {rawCode}）",
                ErrorCode.OnlineStatusExpired => $"{message}：登录态已失效，请重新登录脱盒账号（错误码 {rawCode}）",
                ErrorCode.NoTimes => $"{message}（错误码 {rawCode}）",
                ErrorCode.Failure when !string.IsNullOrWhiteSpace(error.Entity.Message) =>
                    $"{message}：{error.Entity.Message}（错误码 {rawCode}）",
                _ => $"{message}（错误码 {rawCode}）",
            };
        }

        if (inner is HttpRequestException or System.Net.Sockets.SocketException or TaskCanceledException)
        {
            return "网络不可用或请求超时，请检查网络后重试（" + MessageOf(inner) + "）";
        }

        return string.IsNullOrWhiteSpace(inner.Message) ? "未知错误" : inner.Message;
    }

    private static Exception Unwrap(Exception e)
        => e is AggregateException { InnerException: { } inner } ? Unwrap(inner) : e;

    private static string MessageOf(Exception e)
        => e is AggregateException aggregate && aggregate.InnerException != null
            ? aggregate.InnerException.Message
            : e.Message;

    private static string NameOf(EntityAccount account)
        => string.IsNullOrWhiteSpace(account.Name) ? (account.Account ?? account.Type ?? "账号") : account.Name;
}
