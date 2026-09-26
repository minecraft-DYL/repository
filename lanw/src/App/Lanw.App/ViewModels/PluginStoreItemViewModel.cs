using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lanw.Public.Entities.Plugin;

namespace Lanw.App.ViewModels;

/// <summary>
/// 插件商城列表项（对应原 Vue plugin/PluginStore.vue 的卡片 + Lanw.Public.Entities.Plugin.EntityComponents）。
/// 卡片显示 名称 / 简介 / 发布者 / 下载次数；点击卡片进入详情页，行内「安装」按钮对应 PlugInstoreMessage.Install。
/// </summary>
public sealed partial class PluginStoreItemViewModel : ObservableObject
{
    public PluginStoreItemViewModel(
        EntityComponents entity,
        IAsyncRelayCommand<PluginStoreItemViewModel> installCommand)
    {
        Id = entity.Id ?? string.Empty;
        Name = entity.Name ?? string.Empty;
        ShortDescription = entity.ShortDescription ?? string.Empty;
        Publisher = entity.Publisher ?? string.Empty;
        DownloadCount = entity.DownloadCount ?? 0;
        InstallCommand = installCommand;
    }

    /// <summary>插件 ID。</summary>
    public string Id { get; }

    /// <summary>插件名称。</summary>
    public string Name { get; }

    /// <summary>简短介绍（商城列表用）。</summary>
    public string ShortDescription { get; }

    /// <summary>发布者。</summary>
    public string Publisher { get; }

    /// <summary>下载次数。</summary>
    public long DownloadCount { get; }

    /// <summary>发布者文案（对应 Vue「发布者: xxx」）。</summary>
    public string PublisherText => "发布者: " + Publisher;

    /// <summary>下载次数文案（对应 Vue「下载次数: n」）。</summary>
    public string DownloadCountText => "下载次数: " + DownloadCount;

    /// <summary>安装中（安装按钮的忙碌态，避免重复点击）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstallButtonText))]
    public partial bool IsInstalling { get; set; }

    /// <summary>安装按钮文案。</summary>
    public string InstallButtonText => IsInstalling ? "安装中…" : "安装";

    /// <summary>安装命令（由 PluginStoreViewModel 提供，作用于本卡片）。</summary>
    public IAsyncRelayCommand<PluginStoreItemViewModel> InstallCommand { get; }
}
