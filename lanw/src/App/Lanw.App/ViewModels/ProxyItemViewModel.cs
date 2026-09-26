using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lanw.App.Services;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;

namespace Lanw.App.ViewModels;

/// <summary>
/// 运行中代理列表行（对应原 Vue ProxyManager.vue 的 proxy-table 行：
/// id / nick_name / local_address:local_port / server_name + 复制、关闭操作）。
/// </summary>
public sealed partial class ProxyItemViewModel : ObservableObject
{
    private readonly RunningProxyEntry _entry;

    private readonly Action<ProxyItemViewModel>? _onClosed;

    /// <summary>复制反馈提示（非空时在行内显示）。</summary>
    [ObservableProperty]
    public partial string CopyTip { get; set; }

    public ProxyItemViewModel(RunningProxyEntry entry, Action<ProxyItemViewModel>? onClosed = null)
    {
        _entry = entry;
        _onClosed = onClosed;
        CopyTip = string.Empty;
    }

    /// <summary>代理 ID（RunningProxy.Id）。</summary>
    public int Id => _entry.Id;

    public string IdText => $"#{_entry.Id}";

    /// <summary>游戏昵称（对应 nick_name）。</summary>
    public string NickName => string.IsNullOrWhiteSpace(_entry.NickName) ? "（未命名）" : _entry.NickName;

    /// <summary>本地地址（local_address:local_port，游戏端应连接该地址）。</summary>
    public string LocalEndpoint => $"{_entry.LocalAddress}:{_entry.LocalPort}";

    /// <summary>服务器名称（server_name）。</summary>
    public string ServerName => string.IsNullOrWhiteSpace(_entry.ServerName) ? "（未命名服务器）" : _entry.ServerName;

    /// <summary>转发目标（InterceptorConfig.ForwardAddress:ForwardPort）。</summary>
    public string ForwardDisplay => $"转发 → {_entry.ForwardAddress}:{_entry.ForwardPort}";

    /// <summary>代理类型（对应 is_rental）。</summary>
    public string KindDisplay => _entry.IsRental ? "租凭服" : "网络服";

    /// <summary>详情行：版本 / 游戏 ID / 启动时间。</summary>
    public string DetailDisplay =>
        $"版本 {_entry.ServerVersion} · 游戏ID {(string.IsNullOrWhiteSpace(_entry.GameId) ? "未设置" : _entry.GameId)} · 启动 {_entry.StartedAt:HH:mm:ss}";

    public Visibility CopyTipVisibility => string.IsNullOrEmpty(CopyTip) ? Visibility.Collapsed : Visibility.Visible;

    partial void OnCopyTipChanged(string value)
    {
        OnPropertyChanged(nameof(CopyTipVisibility));
    }

    /// <summary>复制本地地址到剪贴板（对应原 Vue 的复制按钮）。</summary>
    [RelayCommand]
    private void CopyAddress()
    {
        var package = new DataPackage();
        package.SetText(LocalEndpoint);
        Clipboard.SetContent(package);
        CopyTip = "已复制本地地址";
    }

    /// <summary>关闭该代理（回到视图模型统一处理，刷新列表与状态）。</summary>
    [RelayCommand]
    private void Close()
    {
        _onClosed?.Invoke(this);
    }
}
