using CommunityToolkit.Mvvm.ComponentModel;
using Lanw.App.Services;
using Lanw.WPFLauncher.Entities.WPFLauncher.RentalGame;
using Lanw.WPFLauncher.Entities.WPFLauncher.RentalGame.GameCharacters;
using Microsoft.UI.Xaml.Media;

namespace Lanw.App.ViewModels;

/// <summary>
/// 租赁服列表项视图模型（对应原 Vue rental/GameRental.vue 的 .server-item 卡片）。
/// 展示：名称、在线状态（<see cref="EnumServerStatus"/>）、MC 版本、可见性（<see cref="EnumVisibilityStatus"/>）、
/// 在线人数/容量、简介、介绍图。
/// 字段来源：详情来自 Message 层 <see cref="EntityRentalGameDetails"/>，状态/可见性来自列表接口
/// <see cref="EntityRentalGame"/>（该载荷无对应字段时为「未知」，不臆造数据）。
/// </summary>
public sealed partial class RentalGameItemViewModel : ObservableObject
{
    private readonly EntityRentalGameDetails _details;
    private readonly EntityRentalGame? _raw;

    public RentalGameItemViewModel(RentalServerEntry entry)
    {
        _details = entry.Details;
        _raw = entry.Raw;
    }

    /// <summary>服务器 ID（entity_id），点击进详情页时使用。</summary>
    public string EntityId => _details.EntityId;

    /// <summary>服务器名称（详情载荷的 server_name，缺失时回落到列表载荷的 name）。</summary>
    public string Name
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_details.ServerName))
            {
                return _details.ServerName;
            }

            return string.IsNullOrWhiteSpace(_raw?.Name) ? "(未命名租赁服)" : _raw!.Name;
        }
    }

    /// <summary>简介（详情载荷 brief_summary，缺失时回落到列表载荷）。</summary>
    public string BriefSummary
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_details.BriefSummary))
            {
                return _details.BriefSummary;
            }

            return string.IsNullOrWhiteSpace(_raw?.BriefSummary) ? "暂无简介" : _raw!.BriefSummary!;
        }
    }

    /// <summary>MC 版本。</summary>
    public string VersionDisplay => string.IsNullOrWhiteSpace(_details.McVersion) ? "版本未知" : _details.McVersion;

    /// <summary>在线人数/容量（对应 Vue 「在线: player_count/capacity」）。</summary>
    public string OnlineCountDisplay => $"在线 {_details.PlayerCount}/{_details.Capacity}";

    /// <summary>在线状态文案（<see cref="EnumServerStatus"/>）。</summary>
    public string StatusText => RentalGameDisplay.ServerStatusText(_raw?.Status);

    /// <summary>
    /// 运行中（ServerOn）时显示状态圆点；其余状态（含状态未知）不显示，避免误判为在线。
    /// </summary>
    public Visibility OnlineDotVisibility
        => _raw?.Status == EnumServerStatus.ServerOn ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>可见性文案（<see cref="EnumVisibilityStatus"/>）。</summary>
    public string VisibilityText => RentalGameDisplay.VisibilityText(_raw?.Visibility);

    /// <summary>服务器类型（vanilla / rental 等，缺失时显示占位）。</summary>
    public string ServerTypeDisplay => string.IsNullOrWhiteSpace(_details.ServerType) ? "类型未知" : _details.ServerType!;

    /// <summary>介绍图（远端 URL 或本地缓存文件；无图时为 null 显示占位图标）。</summary>
    public ImageSource? TitleImage => ServerImageLoader.Create(_details.ImageUrl);

    /// <summary>无介绍图时显示占位图标。</summary>
    public Visibility PlaceholderVisibility => TitleImage is null ? Visibility.Visible : Visibility.Collapsed;
}

/// <summary>
/// 租赁服玩家（游戏角色）列表项视图模型（对应原 Vue 的 games 下拉项）。
/// 字段来源：<see cref="EntityRentalGamePlayerList"/>。
/// </summary>
public sealed partial class RentalPlayerItemViewModel : ObservableObject
{
    private readonly EntityRentalGamePlayerList _entity;

    public RentalPlayerItemViewModel(EntityRentalGamePlayerList entity)
    {
        _entity = entity;
    }

    /// <summary>角色名称（对应 Vue 的 games[].name）。</summary>
    public string Name => string.IsNullOrWhiteSpace(_entity.Name) ? "(未命名角色)" : _entity.Name;

    /// <summary>角色 ID（entity_id）。</summary>
    public string EntityId => _entity.EntityId;

    /// <summary>所属账号 user_id。</summary>
    public string UserIdDisplay => string.IsNullOrWhiteSpace(_entity.UserId) ? "账号未知" : _entity.UserId;

    /// <summary>是否在线（is_online）。</summary>
    public string OnlineText => _entity.IsOnline ? "在线" : "离线";

    /// <summary>在线时显示圆点。</summary>
    public Visibility OnlineDotVisibility => _entity.IsOnline ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>创建时间（create_ts 为 Unix 秒）。</summary>
    public string CreatedAtDisplay => RentalGameDisplay.UnixSecondsText(_entity.CreateTs);
}

/// <summary>
/// 租赁服展示口径（枚举 → 中文文案、时间戳格式化）：集中放置，列表页与详情页共用，避免两处口径漂移。
/// </summary>
internal static class RentalGameDisplay
{
    /// <summary>服务器状态文案（<see cref="EnumServerStatus"/>；null 表示接口未返回状态）。</summary>
    public static string ServerStatusText(EnumServerStatus? status) => status switch
    {
        EnumServerStatus.ServerOff => "已关服",
        EnumServerStatus.ServerOn => "运行中",
        EnumServerStatus.Uninitialized => "未初始化",
        EnumServerStatus.Opening => "开服中",
        EnumServerStatus.Closing => "关服中",
        EnumServerStatus.OutOfDate => "版本过旧",
        EnumServerStatus.SaveCleaning => "存档清理中",
        EnumServerStatus.Resetting => "重置中",
        EnumServerStatus.Upgrading => "升级中",
        EnumServerStatus.DiscOverflow => "磁盘已满",
        EnumServerStatus.None => "状态未知",
        _ => "状态未知",
    };

    /// <summary>可见性文案（<see cref="EnumVisibilityStatus"/>；null 表示接口未返回可见性）。</summary>
    public static string VisibilityText(EnumVisibilityStatus? visibility) => visibility switch
    {
        EnumVisibilityStatus.Public => "公开",
        EnumVisibilityStatus.Friend => "好友可见",
        EnumVisibilityStatus.Private => "私有",
        EnumVisibilityStatus.Password => "密码可见",
        _ => "可见性未知",
    };

    /// <summary>Unix 秒 → 本地时间文本（越界或为 0 时显示占位，不抛异常）。</summary>
    public static string UnixSecondsText(ulong unixSeconds)
    {
        if (unixSeconds == 0 || unixSeconds > long.MaxValue)
        {
            return "未知";
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds((long)unixSeconds).LocalDateTime.ToString("yyyy-MM-dd HH:mm");
        }
        catch (ArgumentOutOfRangeException)
        {
            return "未知";
        }
    }
}
