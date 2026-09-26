using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lanw.App.Services;
using Lanw.WPFLauncher.Entities.WPFLauncher.RentalGame;
using Lanw.WPFLauncher.Entities.WPFLauncher.RentalGame.GameCharacters;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace Lanw.App.ViewModels;

/// <summary>多线接入地址行（移动/电信/联通），供详情页「服务器地址」区展示。</summary>
public sealed record RentalAddressLine(string Label, string Value);

/// <summary>
/// 租赁服详情页视图模型（对应原 Vue rental/GameRentalDetail.vue 的信息区）。
/// 数据链路：RentalGameService → Lanw.WPFLauncher.NPFLauncher 租赁服协议（进程内直调，无 HTTP）：
/// 详情 <see cref="EntityRentalGameDetails"/>、连接地址 <see cref="EntityRentalGameServerAddress"/>、
/// 玩家（游戏角色）列表 <see cref="EntityRentalGamePlayerList"/>。
/// 详情为主数据（失败进入错误态，可页内重试）；地址与玩家列表为次要数据，单独失败只做降级提示，不影响详情展示。
/// 注：原 Vue 的 Launch / 启动代理弹窗与「添加名称」（创建租赁服角色）属于启动流程，由后续任务移植，本页不涉及。
/// </summary>
public sealed partial class GameRentalDetailViewModel : ObservableObject
{
    private readonly RentalGameService _service;

    /// <summary>多线接入地址行（isp_enable 为真时展示）。</summary>
    public ObservableCollection<RentalAddressLine> AddressLines { get; } = [];

    /// <summary>玩家（游戏角色）列表。</summary>
    public ObservableCollection<RentalPlayerItemViewModel> Players { get; } = [];

    [ObservableProperty]
    public partial string ServerId { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string VersionDisplay { get; set; }

    [ObservableProperty]
    public partial string ServerTypeDisplay { get; set; }

    [ObservableProperty]
    public partial string OnlineCountDisplay { get; set; }

    [ObservableProperty]
    public partial string CapacityDisplay { get; set; }

    [ObservableProperty]
    public partial string BriefSummary { get; set; }

    [ObservableProperty]
    public partial string Address { get; set; }

    [ObservableProperty]
    public partial string AddressDisplay { get; set; }

    [ObservableProperty]
    public partial string AddressStateText { get; set; }

    [ObservableProperty]
    public partial string AddressNote { get; set; }

    [ObservableProperty]
    public partial string PlayersNote { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial ImageSource? MainImage { get; set; }

    [ObservableProperty]
    public partial Visibility MainImagePlaceholderVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility LoadingVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility ErrorVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility ContentVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility AddressActionsVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility AddressLinesVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility PlayersVisibility { get; set; }

    /// <summary>玩家区提示文案的可见性（加载中 / 有角色 / 无角色 / 失败都显示，避免失败时无提示）。</summary>
    [ObservableProperty]
    public partial Visibility PlayersNoteVisibility { get; set; }

    public GameRentalDetailViewModel(RentalGameService? service = null)
    {
        _service = service ?? new RentalGameService();
        ServerId = string.Empty;
        Name = string.Empty;
        VersionDisplay = string.Empty;
        ServerTypeDisplay = string.Empty;
        OnlineCountDisplay = string.Empty;
        CapacityDisplay = string.Empty;
        BriefSummary = string.Empty;
        Address = string.Empty;
        AddressDisplay = string.Empty;
        AddressStateText = string.Empty;
        AddressNote = string.Empty;
        PlayersNote = string.Empty;
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;
        MainImagePlaceholderVisibility = Visibility.Visible;
        LoadingVisibility = Visibility.Collapsed;
        ErrorVisibility = Visibility.Collapsed;
        ContentVisibility = Visibility.Collapsed;
        AddressActionsVisibility = Visibility.Collapsed;
        AddressLinesVisibility = Visibility.Collapsed;
        PlayersVisibility = Visibility.Collapsed;
        PlayersNoteVisibility = Visibility.Collapsed;
    }

    /// <summary>进入详情页时由页面调用：记录服务器 ID 并加载详情 + 地址 + 玩家列表（也是错误态「重试」入口）。</summary>
    [RelayCommand]
    private async Task LoadAsync(string? serverId)
    {
        var id = string.IsNullOrWhiteSpace(serverId) ? ServerId : serverId.Trim();
        if (string.IsNullOrWhiteSpace(id) || IsLoading)
        {
            return;
        }

        ServerId = id;
        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;
        LoadingVisibility = Visibility.Visible;
        ErrorVisibility = Visibility.Collapsed;
        StatusMessage = "正在获取租赁服详情…";
        try
        {
            var details = await _service.GetServerDetailAsync(id).ConfigureAwait(true);
            Apply(details);
            ContentVisibility = Visibility.Visible;

            // 地址 / 玩家列表为次要数据：各自降级，不影响详情主体
            await LoadAddressAsync(id).ConfigureAwait(true);
            await LoadPlayersAsync(id).ConfigureAwait(true);

            StatusMessage = "租赁服详情已加载";
        }
        catch (Exception e)
        {
            // 无网络 / 接口异常：进入错误态展示，不向 UI 线程抛出（避免启动器崩溃）
            ResetContent();
            HasError = true;
            ErrorMessage = "无法获取租赁服详情：" + MessageOf(e);
            StatusMessage = ErrorMessage;
        }
        finally
        {
            IsLoading = false;
            LoadingVisibility = Visibility.Collapsed;
            ErrorVisibility = HasError ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>刷新玩家列表（页内独立入口；不重新拉详情）。</summary>
    [RelayCommand]
    private async Task ReloadPlayersAsync()
    {
        if (string.IsNullOrWhiteSpace(ServerId))
        {
            return;
        }

        await LoadPlayersAsync(ServerId).ConfigureAwait(true);
    }

    /// <summary>一键复制连接地址。</summary>
    [RelayCommand]
    private void CopyAddress()
    {
        if (string.IsNullOrWhiteSpace(Address))
        {
            StatusMessage = "暂无服务器地址可复制";
            return;
        }

        var package = new DataPackage();
        package.SetText(Address);
        Clipboard.SetContent(package);
        StatusMessage = $"已复制服务器地址：{Address}";
    }

    /// <summary>拉取连接地址（含三线接入）：失败仅降级提示，不清空详情。</summary>
    private async Task LoadAddressAsync(string serverId)
    {
        AddressNote = "正在获取服务器地址…";
        try
        {
            // 参考实现（原 Vue 详情页 / getRentalInfo）同样不向地址接口传密码；私有/密码可见服可能因此取不到地址。
            var address = await _service.GetServerAddressAsync(serverId).ConfigureAwait(true);
            ApplyAddress(address);
            AddressNote = string.Empty;
        }
        catch (Exception e)
        {
            Address = string.Empty;
            AddressDisplay = "未获取到连接地址";
            AddressStateText = "未知";
            AddressLines.Clear();
            AddressLinesVisibility = Visibility.Collapsed;
            AddressActionsVisibility = Visibility.Collapsed;
            AddressNote = "获取服务器地址失败（需要密码的租赁服可能无法直接获取）：" + MessageOf(e);
        }
    }

    /// <summary>拉取玩家（游戏角色）列表：失败仅降级提示，不清空详情。</summary>
    private async Task LoadPlayersAsync(string serverId)
    {
        PlayersNote = "正在获取玩家列表…";
        PlayersNoteVisibility = Visibility.Visible;
        try
        {
            var players = await _service.GetPlayersAsync(serverId).ConfigureAwait(true);
            Players.Clear();
            foreach (var player in players)
            {
                Players.Add(new RentalPlayerItemViewModel(player));
            }

            PlayersVisibility = Players.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            PlayersNote = Players.Count == 0
                ? "该租赁服上没有当前账号的游戏角色（接口口径：按账号+服务器查询）"
                : $"共 {Players.Count} 个游戏角色";
        }
        catch (Exception e)
        {
            Players.Clear();
            PlayersVisibility = Visibility.Collapsed;
            PlayersNote = "获取玩家列表失败：" + MessageOf(e);
        }
    }

    private void Apply(EntityRentalGameDetails details)
    {
        Name = string.IsNullOrWhiteSpace(details.ServerName) ? "(未命名租赁服)" : details.ServerName;
        VersionDisplay = string.IsNullOrWhiteSpace(details.McVersion) ? "未知" : details.McVersion;
        ServerTypeDisplay = string.IsNullOrWhiteSpace(details.ServerType) ? "未知" : details.ServerType!;
        OnlineCountDisplay = details.PlayerCount.ToString();
        CapacityDisplay = details.Capacity.ToString();
        BriefSummary = string.IsNullOrWhiteSpace(details.BriefSummary) ? "暂无服务器介绍" : details.BriefSummary;

        MainImage = ServerImageLoader.Create(details.ImageUrl);
        MainImagePlaceholderVisibility = MainImage is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyAddress(EntityRentalGameServerAddress address)
    {
        Address = string.IsNullOrWhiteSpace(address.McServerHost)
            ? string.Empty
            : $"{address.McServerHost}:{address.McServerPort}";
        AddressDisplay = string.IsNullOrWhiteSpace(Address) ? "未获取到连接地址" : Address;
        AddressActionsVisibility = string.IsNullOrWhiteSpace(Address) ? Visibility.Collapsed : Visibility.Visible;
        AddressStateText = RentalGameDisplay.ServerStatusText(address.State);

        AddressLines.Clear();
        AddAddressLine("移动", address.CmccMcServerHost, address.CmccMcServerPort);
        AddAddressLine("电信", address.CtccMcServerHost, address.CtccMcServerPort);
        AddAddressLine("联通", address.CuccMcServerHost, address.CuccMcServerPort);
        AddressLinesVisibility = AddressLines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddAddressLine(string label, string host, int port)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return;
        }

        AddressLines.Add(new RentalAddressLine(label, $"{host}:{port}"));
    }

    /// <summary>错误态下清空内容，避免展示上一台的残留数据。</summary>
    private void ResetContent()
    {
        AddressLines.Clear();
        Players.Clear();
        MainImage = null;
        MainImagePlaceholderVisibility = Visibility.Visible;
        ContentVisibility = Visibility.Collapsed;
        AddressActionsVisibility = Visibility.Collapsed;
        AddressLinesVisibility = Visibility.Collapsed;
        PlayersVisibility = Visibility.Collapsed;
        PlayersNoteVisibility = Visibility.Collapsed;
    }

    private static string MessageOf(Exception e)
        => e is AggregateException aggregate && aggregate.InnerException != null
            ? aggregate.InnerException.Message
            : e.Message;
}
