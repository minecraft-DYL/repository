using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lanw.Core.Entities.Login;
using Microsoft.UI.Xaml;

namespace Lanw.App.ViewModels;

/// <summary>
/// 账号列表行视图模型：封装单个 EntityAccount 的展示与行内命令。
/// </summary>
public sealed partial class AccountItemViewModel : ObservableObject
{
    private readonly AccountsViewModel _owner;

    /// <summary>底层账号实体（来自进程内持久化存储）。</summary>
    public EntityAccount Account { get; }

    /// <summary>账号 Id（位置语义，读自持久化）。</summary>
    public int Id => Account.Id ?? -1;

    /// <summary>展示账号（cookie 型用 Name 生成假号）。</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Account.Name) ? (Account.Account ?? "未命名") : Account.Name;

    /// <summary>账号登录类型文案。</summary>
    public string LoginType => Account.Type ?? "未知";

    /// <summary>掩码后的 UserId（敏感信息脱敏）。</summary>
    public string SafeUserId => Mask(Account.UserId);

    /// <summary>是否已登录（持有 token）；安全可见性。</summary>
    public Visibility SafeUserIdVisibility => Account.IsNotNuLl() ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>是否当前账号。</summary>
    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    /// <summary>当前徽标可见性（跟随 IsCurrent）。</summary>
    [ObservableProperty]
    public partial Visibility CurrentBadgeVisibility { get; set; }

    public AccountItemViewModel(AccountsViewModel owner, EntityAccount account)
    {
        _owner = owner;
        Account = account;
        IsCurrent = owner.Repository.IsCurrent(account);
        CurrentBadgeVisibility = ToVisibility(IsCurrent);
    }

    /// <summary>删除该账号。</summary>
    [RelayCommand]
    private void Delete() => _owner.DeleteAccount(this);

    /// <summary>设为当前账号（进程内，不触发登录协议）。</summary>
    [RelayCommand]
    private void SetCurrent() => _owner.SetCurrentAccount(this);

    /// <summary>切换账号入口（进程内加载该账号为当前，登录协议留待 t7c）。</summary>
    [RelayCommand]
    private void Switch() => _owner.SwitchToAccount(this);

    /// <summary>编辑该账号（对应原版行内「编辑」按钮；弹窗由视图注入，见 AccountsViewModel.EditPrompt）。</summary>
    [RelayCommand]
    private void Edit() => _owner.RequestEditAccount(this);

    /// <summary>由父 VM 通知当前态变化后刷新。</summary>
    public void RefreshIsCurrent()
    {
        IsCurrent = _owner.Repository.IsCurrent(Account);
        CurrentBadgeVisibility = ToVisibility(IsCurrent);
    }

    /// <summary>在 IsCurrent 发生变化后由 [NotifyPropertyChangedFor] 方式也可，此处用局部刷新保证徽标同步。</summary>
    partial void OnIsCurrentChanged(bool value)
    {
        CurrentBadgeVisibility = ToVisibility(value);
    }

    private static Visibility ToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    private static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        return value.Length <= 4 ? value : value[..4] + "…";
    }
}