using CommunityToolkit.Mvvm.ComponentModel;
using Lanw.App.Services;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameSkin;
using Microsoft.UI.Xaml.Media;

namespace Lanw.App.ViewModels;

/// <summary>
/// 皮肤列表项视图模型（对应原 Vue skin/Skins.vue 的 .skin-item 卡片）：
/// 预览图 + 名称 + 简介 + 开发者/下载量/点赞数。
/// </summary>
public sealed partial class SkinItemViewModel : ObservableObject
{
    public SkinItemViewModel(EntityQueryNetSkinItem entity)
    {
        Entity = entity;
    }

    /// <summary>原始实体（点击进详情页时取 EntityId）。</summary>
    public EntityQueryNetSkinItem Entity { get; }

    public string EntityId => Entity.EntityId;

    public string Name => string.IsNullOrWhiteSpace(Entity.Name) ? "(未命名皮肤)" : Entity.Name;

    public string BriefSummary => string.IsNullOrWhiteSpace(Entity.BriefSummary) ? "暂无简介" : Entity.BriefSummary;

    public string DeveloperDisplay => string.IsNullOrWhiteSpace(Entity.DeveloperName)
        ? "开发者: 未知"
        : $"开发者: {Entity.DeveloperName}";

    public string DownloadDisplay => $"下载量: {Entity.DownloadNum}";

    public string LikeDisplay => $"点赞数: {Entity.LikeNum}";

    /// <summary>发布时间（unix 秒 → yyyy-MM-dd；与 EntitySkinDetail 的口径一致）。</summary>
    public string PublishTimeDisplay => Entity.PublishTime <= 0
        ? "发布时间未知"
        : DateTimeOffset.FromUnixTimeSeconds(Entity.PublishTime).ToString("yyyy-MM-dd");

    /// <summary>预览图（远端 URL 或本地缓存文件；无图时为 null，界面显示占位图标）。</summary>
    public ImageSource? TitleImage => SkinImageLoader.Create(Entity.TitleImageUrl);

    /// <summary>无预览图时显示占位图标。</summary>
    public Visibility PlaceholderVisibility => TitleImage is null ? Visibility.Visible : Visibility.Collapsed;
}
