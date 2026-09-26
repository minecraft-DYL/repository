using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lanw.App.Services;
using Microsoft.UI.Xaml;

namespace Lanw.App.ViewModels;

/// <summary>
/// 代理页视图模型（对应原 Vue ProxyManager.vue，并在原页面基础上补出「拦截器配置」卡片）：
/// 1) 拦截器配置：监听地址/端口、转发地址/端口、昵称、服务器名称/版本、游戏 ID、租凭服标记、脱盒拦截开关；
/// 2) 运行状态：当前运行的代理数量 + 列表（ID/昵称/本地地址/服务器名称/转发目标/操作）；
/// 3) 操作：保存配置 / 重新载入 / 恢复默认 / 启动本机拦截器 / 复制地址 / 关闭单个 / 关闭全部。
/// 数据链路：进程内直调 —— Lanw.Development.Interceptor（t20）+ Lanw.Public.Entities.NEL.RunningProxy（t14）
/// + Lanw.Core.Utils.ConfigUtil（配置持久化，resources/config.json）。无 HTTP。
/// </summary>
public sealed partial class ProxyManagerViewModel : ObservableObject
{
    private readonly AccountRepository _accounts = new();

    private bool _activated;

    /// <summary>脱盒拦截开关（InterceptorConfig 是否生效；关闭后不允许启动新的拦截器）。</summary>
    [ObservableProperty]
    public partial bool InterceptEnabled { get; set; }

    /// <summary>代理监听地址（InterceptorConfig.LocalAddress）。</summary>
    [ObservableProperty]
    public partial string LocalAddress { get; set; }

    /// <summary>代理监听端口下限（InterceptorConfig.LocalPort，占用时自动后移）。</summary>
    [ObservableProperty]
    public partial double LocalPort { get; set; }

    /// <summary>转发目标地址（InterceptorConfig.ForwardAddress）。</summary>
    [ObservableProperty]
    public partial string ForwardAddress { get; set; }

    /// <summary>转发目标端口（InterceptorConfig.ForwardPort）。</summary>
    [ObservableProperty]
    public partial double ForwardPort { get; set; }

    /// <summary>替换后的游戏昵称（InterceptorConfig.NickName）。</summary>
    [ObservableProperty]
    public partial string NickName { get; set; }

    /// <summary>服务器名称（InterceptorConfig.ServerName）。</summary>
    [ObservableProperty]
    public partial string ServerName { get; set; }

    /// <summary>服务器版本（InterceptorConfig.ServerVersion）。</summary>
    [ObservableProperty]
    public partial string ServerVersion { get; set; }

    /// <summary>模组信息（InterceptorConfig.ModInfo）。</summary>
    [ObservableProperty]
    public partial string ModInfo { get; set; }

    /// <summary>游戏/服务器 ID（InterceptorConfig.GameId）。</summary>
    [ObservableProperty]
    public partial string GameId { get; set; }

    /// <summary>租凭服代理（InterceptorConfig.IsRental）。</summary>
    [ObservableProperty]
    public partial bool IsRental { get; set; }

    /// <summary>状态提示。</summary>
    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    /// <summary>当前账号展示（启动代理需要已登录账号）。</summary>
    [ObservableProperty]
    public partial string CurrentAccountDisplay { get; set; }

    /// <summary>是否正在执行启动等耗时操作。</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>当前运行的代理数量。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountText))]
    [NotifyPropertyChangedFor(nameof(HasProxiesVisibility))]
    [NotifyPropertyChangedFor(nameof(EmptyVisibility))]
    public partial int ProxyCount { get; set; }

    /// <summary>运行中代理列表。</summary>
    public ObservableCollection<ProxyItemViewModel> Proxies { get; } = [];

    /// <summary>数量展示（对应原 Vue 的「当前运行的代理数量」）。</summary>
    public string CountText => $"当前运行的代理数量：{ProxyCount}";

    public Visibility HasProxiesVisibility => ProxyCount > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyVisibility => ProxyCount > 0 ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>配置文件路径（状态提示用）。</summary>
    public string ConfigFilePath => ProxyConfigStore.ConfigFilePath;

    public ProxyManagerViewModel()
    {
        // 先给出非空初值（随后 LoadCore 会覆盖），避免可空引用警告
        LocalAddress = string.Empty;
        ForwardAddress = string.Empty;
        NickName = string.Empty;
        ServerName = string.Empty;
        ServerVersion = string.Empty;
        ModInfo = string.Empty;
        GameId = string.Empty;
        StatusMessage = string.Empty;
        CurrentAccountDisplay = string.Empty;
        LoadCore();
        RefreshList();
    }

    /// <summary>页面进入：订阅注册表变化 + 刷新列表。</summary>
    public void Activate()
    {
        if (!_activated)
        {
            ProxyService.Current.Changed += OnProxiesChanged;
            _activated = true;
        }

        RefreshCurrentAccount();
        RefreshList();
    }

    /// <summary>页面离开：退订，避免视图模型被注册表长期引用。</summary>
    public void Deactivate()
    {
        if (_activated)
        {
            ProxyService.Current.Changed -= OnProxiesChanged;
            _activated = false;
        }
    }

    /// <summary>保存配置到 resources/config.json。</summary>
    [RelayCommand]
    private void Save()
    {
        if (!BuildSettingsIfPossible(out var settings, out var error))
        {
            StatusMessage = $"保存失败：{error}";
            return;
        }

        try
        {
            ProxyConfigStore.Save(settings);
            ApplySettings(settings);
            StatusMessage = $"已保存代理配置（{ConfigFilePath}，重启后仍然生效）";
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存失败：{ex.Message}";
        }
    }

    /// <summary>重新载入已保存配置。</summary>
    [RelayCommand]
    private void Reload()
    {
        LoadCore();
        StatusMessage = "已重新载入已保存的代理配置";
    }

    /// <summary>恢复默认值（仅填入界面，需点「保存配置」后生效）。</summary>
    [RelayCommand]
    private void ResetDefaults()
    {
        ApplySettings(new ProxySettings());
        StatusMessage = "已恢复默认值，点击「保存配置」后生效";
    }

    /// <summary>刷新运行状态列表（含当前账号）。</summary>
    [RelayCommand]
    private void RefreshList()
    {
        RefreshCurrentAccount();

        var snapshot = ProxyService.Current.GetAllProxies();
        Proxies.Clear();
        foreach (var entry in snapshot)
        {
            Proxies.Add(new ProxyItemViewModel(entry, OnItemCloseRequested));
        }

        ProxyCount = Proxies.Count;
    }

    /// <summary>关闭全部代理（对应原 Vue 的「关闭全部代理」）。</summary>
    [RelayCommand]
    private void CloseAllProxies()
    {
        var closed = ProxyService.Current.CloseAll();
        RefreshList();
        StatusMessage = closed > 0 ? $"已关闭全部代理（{closed} 个）" : "当前没有运行的代理服务器";
    }

    /// <summary>启动本机脱盒拦截器（进程内直调 t20 的 Interceptor.CreateInterceptor）。</summary>
    [RelayCommand]
    private async Task StartInterceptorAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!InterceptEnabled)
        {
            StatusMessage = "脱盒拦截开关已关闭：请先开启拦截再启动代理";
            return;
        }

        var account = _accounts.GetCurrent();
        if (account == null)
        {
            StatusMessage = "启动失败：请先在「登录」页登录账号（拦截器需要携带账号信息）";
            return;
        }

        if (!BuildSettingsIfPossible(out var settings, out var error))
        {
            StatusMessage = $"启动失败：{error}";
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.NickName))
        {
            settings.NickName = string.IsNullOrWhiteSpace(account.Name) ? account.Account ?? string.Empty : account.Name!;
        }

        if (string.IsNullOrWhiteSpace(settings.NickName))
        {
            StatusMessage = "启动失败：请填写游戏昵称（拦截后登录包会被改写为该昵称）";
            return;
        }

        IsBusy = true;
        StatusMessage = "正在启动拦截器…";
        try
        {
            var entry = await Task.Run(() => ProxyService.Current.Start(settings, account)).ConfigureAwait(true);
            RefreshList();
            StatusMessage = $"已启动代理" +
                            $"（本地 {entry.LocalAddress}:{entry.LocalPort} → 转发 {entry.ForwardAddress}:{entry.ForwardPort}，" +
                            $"昵称 {entry.NickName}）。游戏端连接本地地址即可，配置来自 {ConfigFilePath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"启动失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>复制配置文件路径。</summary>
    [RelayCommand]
    private void CopyConfigPath()
    {
        var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
        package.SetText(ConfigFilePath);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        StatusMessage = "已复制配置文件路径";
    }

    private void OnItemCloseRequested(ProxyItemViewModel item)
    {
        var closed = ProxyService.Current.Close(item.Id);
        RefreshList();
        StatusMessage = closed ? $"已关闭代理 {item.NickName}（本地 {item.LocalEndpoint}）" : $"代理 {item.NickName} 已不存在";
    }

    private void OnProxiesChanged(object? sender, EventArgs e)
    {
        RefreshList();
    }

    private void LoadCore()
    {
        try
        {
            ApplySettings(ProxyConfigStore.Load());
        }
        catch (Exception ex)
        {
            StatusMessage = $"载入代理配置失败：{ex.Message}";
        }
    }

    private void ApplySettings(ProxySettings settings)
    {
        InterceptEnabled = settings.InterceptEnabled;
        LocalAddress = settings.LocalAddress;
        LocalPort = settings.LocalPort;
        ForwardAddress = settings.ForwardAddress;
        ForwardPort = settings.ForwardPort;
        NickName = settings.NickName;
        ServerName = settings.ServerName;
        ServerVersion = settings.ServerVersion;
        ModInfo = settings.ModInfo;
        GameId = settings.GameId;
        IsRental = settings.IsRental;
    }

    /// <summary>按界面填写值组装配置，并做端口/地址校验。</summary>
    private bool BuildSettingsIfPossible(out ProxySettings settings, out string error)
    {
        settings = new ProxySettings();
        error = string.Empty;

        var localPort = (int)LocalPort;
        var forwardPort = (int)ForwardPort;

        if (string.IsNullOrWhiteSpace(LocalAddress))
        {
            error = "代理监听地址不能为空";
        }
        else if (localPort is < 1 or > 65535)
        {
            error = "本地端口必须在 1~65535 之间";
        }
        else if (string.IsNullOrWhiteSpace(ForwardAddress))
        {
            error = "转发地址不能为空";
        }
        else if (forwardPort is < 1 or > 65535)
        {
            error = "转发端口必须在 1~65535 之间";
        }
        else if (string.IsNullOrWhiteSpace(ServerName))
        {
            error = "服务器名称不能为空（用于 UDP 广播 MOTD）";
        }
        else if (string.IsNullOrWhiteSpace(ServerVersion))
        {
            error = "服务器版本不能为空";
        }

        if (!string.IsNullOrEmpty(error))
        {
            return false;
        }

        settings.InterceptEnabled = InterceptEnabled;
        settings.LocalAddress = LocalAddress.Trim();
        settings.LocalPort = localPort;
        settings.ForwardAddress = ForwardAddress.Trim();
        settings.ForwardPort = forwardPort;
        settings.NickName = NickName.Trim();
        settings.ServerName = ServerName.Trim();
        settings.ServerVersion = ServerVersion.Trim();
        settings.ModInfo = ModInfo.Trim();
        settings.GameId = GameId.Trim();
        settings.IsRental = IsRental;
        return true;
    }

    private void RefreshCurrentAccount()
    {
        try
        {
            var account = _accounts.GetCurrent();
            CurrentAccountDisplay = account == null
                ? "未登录（启动代理前请先在「登录」页登录）"
                : $"当前账号：{account.Name ?? account.Account ?? account.UserId ?? "已登录"}";
        }
        catch (Exception)
        {
            CurrentAccountDisplay = "未登录（启动代理前请先在「登录」页登录）";
        }
    }
}
