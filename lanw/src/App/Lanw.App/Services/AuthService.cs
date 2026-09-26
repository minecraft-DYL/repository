using Lanw.Core.Entities.Login;
using Lanw.Core.Manager;
using Lanw.Public.Message;

namespace Lanw.App.Services;

/// <summary>
/// 认证接线服务：把登录表单/账号管理页操作接到 t6 的账号服务与登录编排。
/// 包裹 Lanw.Public.AccountMessage（登录协议）与 Lanw.Core.InfoManager（当前登录态）。
/// 网络/协议调用属于同步阻塞，统一放到后台线程执行以避免冻结 UI。
/// </summary>
public sealed class AuthService
{
    private readonly AccountRepository _repository;

    public AuthService(AccountRepository? repository = null)
    {
        _repository = repository ?? new AccountRepository();
    }

    /// <summary>当前登录账号（无则 null）。</summary>
    public EntityAccount? GetCurrent()
    {
        try
        {
            return InfoManager.GetGameAccount();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>是否处于已登录态。</summary>
    public bool IsLoggedIn => GetCurrent() is { Token: not null, UserId: not null };

    /// <summary>
    /// 执行登录：提交账号 -> 调用对应协议（AutoLogin1 按 Type 分发 cookie/4399/4399com/163Email） -> 返回带 token 的当前账号。
    /// 注意：4399 / 4399com 走此入口时会自动拉取并自动识别验证码。
    /// </summary>
    public Task<EntityAccount?> LoginAsync(EntityAccount pending)
        => Task.Run(() =>
        {
            AccountMessage.AutoLogin1(pending);
            return GetCurrent();
        });

    /// <summary>
    /// 拉取一张新的 4399 验证码图片（同时建立验证码会话，供手动验证码登录使用）。
    /// 网络调用，后台线程执行。
    /// </summary>
    public Task<byte[]?> LoadCaptcha4399Async()
        => Task.Run(() =>
        {
            AccountMessage.UpdateCaptcha();
            return AccountMessage.Captcha4399Bytes;
        });

    /// <summary>
    /// 使用用户手动输入的验证码登录（4399 / 4399com）。
    /// 必须先用 <see cref="LoadCaptcha4399Async"/> 建立验证码会话，否则协议会抛“没有验证”。
    /// </summary>
    public Task<EntityAccount?> LoginWithCaptchaAsync(EntityAccount pending, string captcha)
        => Task.Run(() =>
        {
            AccountMessage.LoginWithCaptcha4399(pending, captcha);
            return GetCurrent();
        });

    /// <summary>
    /// 切换账号到指定 Id（真实切换：AccountMessage.SwitchAccountToForce）。
    /// </summary>
    public Task<EntityAccount?> SwitchAsync(int id)
        => Task.Run(() =>
        {
            AccountMessage.SwitchAccountToForce(id);
            return GetCurrent();
        });

    /// <summary>
    /// 切换并自动登录：已登录则设当前，否则走登录协议。
    /// </summary>
    public Task<EntityAccount?> SwitchAndLoginAsync(EntityAccount account)
        => Task.Run(() =>
        {
            AccountMessage.AutoSwitchAccount(account);
            return GetCurrent();
        });

    /// <summary>
    /// 启动自动登录"上次活跃账号"（账号列表首个可登录账号；已登录则设当前，否则走协议）。
    /// </summary>
    public Task<EntityAccount?> AutoLoginLastActiveAsync()
        => Task.Run(() =>
        {
            var list = _repository.Load();
            var lastActive = list.FirstOrDefault(static a => a is { UserId: not null, Token: not null })
                             ?? list.FirstOrDefault();
            if (lastActive is null)
            {
                return GetCurrent();
            }

            AccountMessage.AutoSwitchAccount(lastActive);
            return GetCurrent();
        });

    /// <summary>
    /// 注销退出：清除当前登录态（内存态，InfoManager）。
    /// </summary>
    public void Logout()
    {
        var current = GetCurrent();
        if (current is not null)
        {
            foreach (var match in InfoManager.GameAccountList.Where(g => g.Equals(current)).ToList())
            {
                InfoManager.GameAccountList.Remove(match);
            }
        }

        InfoManager.SetGameAccount(null);
    }
}
