using CommunityToolkit.Mvvm.ComponentModel;
using Lanw.App.Services;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame;
using Microsoft.UI.Xaml.Media;

namespace Lanw.App.ViewModels;

/// <summary>
/// 服务器列表项视图模型（对应原 Vue Servers.vue 的 .server-item 卡片）。
/// 展示 X19 列表接口下发的字段：图标、名称、简介、在线人数、MC 版本。
/// 说明：玩法模式 / 细分分类不在 /item/query/available 的实体载荷里（EntityNetGameItem 仅有
/// entity_id / name / brief_summary / online_count / title_image_url / version），
/// 故分类以查询口径（master_type_id=2 网络服）标注，不臆造字段。
/// </summary>
public sealed partial class ServerItemViewModel : ObservableObject
{
    public ServerItemViewModel(EntityNetGameItem entity)
    {
        Entity = entity;
    }

    /// <summary>原始实体（点击进详情页时取 EntityId）。</summary>
    public EntityNetGameItem Entity { get; }

    public string EntityId => Entity.EntityId;

    public string Name => string.IsNullOrWhiteSpace(Entity.Name) ? "(未命名服务器)" : Entity.Name;

    public string BriefSummary => string.IsNullOrWhiteSpace(Entity.BriefSummary) ? "暂无简介" : Entity.BriefSummary;

    /// <summary>在线人数（网易接口可能返回 "1.2万" 之类的文本，直接展示）。</summary>
    public string OnlineCountDisplay => string.IsNullOrWhiteSpace(Entity.OnlineCount) ? "在线未知" : $"在线 {Entity.OnlineCount}";

    /// <summary>MC 版本（版本来自详情页补全，缺失时显示占位）。</summary>
    public string VersionDisplay => string.IsNullOrWhiteSpace(Entity.Version) ? "版本未知" : Entity.Version;

    /// <summary>分类标签（本次查询口径固定为网络服）。</summary>
    public string CategoryTag => "网络服";

    /// <summary>介绍图（远端 URL 或本地缓存文件；无图时为 null 显示占位图标）。</summary>
    public ImageSource? TitleImage => ServerImageLoader.Create(Entity.TitleImageUrl);

    /// <summary>无介绍图时显示占位图标。</summary>
    public Visibility PlaceholderVisibility => TitleImage is null ? Visibility.Visible : Visibility.Collapsed;
}
