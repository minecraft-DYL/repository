using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lanw.App.Services;
using Lanw.Public.Entities.Plugin;

namespace Lanw.App.ViewModels;

/// <summary>
/// 插件详情页视图模型（对应原 Vue plugin/PluginDetail.vue + PluginsShopController 的
/// /api/pluginstore/detail | install）。
/// 数据链路：PluginService → Lanw.Public.Message.PlugInstoreMessage.GetPluginDetail / Install（进程内直调，无 HTTP）。
/// 展示：名称 / 插件 ID / 发布者 / 发布时间 / 插件版本 / 下载数量 / 依赖项 / 插件介绍；
/// 操作：Download（安装）—— 安装前由页面弹确认框，安装会一并安装 EntityPluginDependency 依赖。
/// </summary>
public sealed partial class PluginDetailViewModel : ObservableObject
{
    private readonly PluginService _service;
    private string _pluginId = string.Empty;

    /// <summary>依赖项（对应 Vue plugin.dependencies，EntityPluginDependency）。</summary>
    public ObservableCollection<PluginDependencyViewModel> Dependencies { get; } = [];

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string PluginId { get; set; }

    [ObservableProperty]
    public partial string Publisher { get; set; }

    [ObservableProperty]
    public partial string PublishDate { get; set; }

    [ObservableProperty]
    public partial string Version { get; set; }

    [ObservableProperty]
    public partial string DownloadCount { get; set; }

    [ObservableProperty]
    public partial string ShortDescription { get; set; }

    /// <summary>插件介绍（原 Vue 用 v-html 渲染 HTML，桌面端以纯文本展示，避免脚本注入）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DescriptionVisibility))]
    [NotifyPropertyChangedFor(nameof(NoDescriptionVisibility))]
    public partial string DetailDescription { get; set; }

    [ObservableProperty]
    public partial string LogoUrl { get; set; }

    /// <summary>是否需要显示 Logo（logoUrl 非空）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LogoVisibility))]
    public partial bool HasLogo { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstallButtonText))]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    public partial bool IsInstalling { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    /// <summary>安装确认文案（对应 Vue confirmMessage：含依赖时列出依赖名）。</summary>
    [ObservableProperty]
    public partial string ConfirmMessage { get; set; }

    /// <summary>依赖项为空时的展示文案（对应 Vue：「无依赖」）。</summary>
    [ObservableProperty]
    public partial string DependenciesHint { get; set; }

    [ObservableProperty]
    public partial Visibility ContentVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility LoadingVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility ErrorVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility DependenciesVisibility { get; set; }

    /// <summary>插件介绍可见性（为空时显示占位文案）。</summary>
    public Visibility DescriptionVisibility
        => string.IsNullOrWhiteSpace(DetailDescription) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>无介绍占位可见性（对应 Vue：「这个插件没有介绍自己呢。」）。</summary>
    public Visibility NoDescriptionVisibility
        => string.IsNullOrWhiteSpace(DetailDescription) ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Logo 可见性。</summary>
    public Visibility LogoVisibility => HasLogo ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>安装按钮文案。</summary>
    public string InstallButtonText => IsInstalling ? "安装中…" : "Download";

    /// <summary>安装按钮可用性（安装中禁用，避免重复提交）。</summary>
    public bool CanInstall => !IsInstalling;

    public PluginDetailViewModel(PluginService? service = null)
    {
        _service = service ?? new PluginService();
        Name = string.Empty;
        PluginId = string.Empty;
        Publisher = string.Empty;
        PublishDate = string.Empty;
        Version = string.Empty;
        DownloadCount = string.Empty;
        ShortDescription = string.Empty;
        DetailDescription = string.Empty;
        LogoUrl = string.Empty;
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;
        ConfirmMessage = string.Empty;
        DependenciesHint = string.Empty;
        ContentVisibility = Visibility.Collapsed;
        LoadingVisibility = Visibility.Collapsed;
        ErrorVisibility = Visibility.Collapsed;
        DependenciesVisibility = Visibility.Collapsed;
    }

    /// <summary>加载插件详情（导航参数为插件 ID）。</summary>
    [RelayCommand]
    private async Task LoadAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            HasError = true;
            ErrorMessage = "缺少插件 ID，无法加载插件详情";
            UpdateViewState();
            return;
        }

        _pluginId = id!;
        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;
        LoadingVisibility = Visibility.Visible;
        ErrorVisibility = Visibility.Collapsed;
        ContentVisibility = Visibility.Collapsed;
        StatusMessage = "正在加载插件详情…";
        try
        {
            var response = await _service.GetPluginDetailAsync(_pluginId).ConfigureAwait(true);

            // 对应 Vue：res.code == 1 才算成功，否则把 msg 作为错误信息
            if (response is { Code: 1, Data: not null })
            {
                Fill(response.Data);
                StatusMessage = "插件详情已加载";
            }
            else
            {
                HasError = true;
                ErrorMessage = "获取插件详情失败：" + (response?.Message ?? "插件不存在或接口无返回");
                StatusMessage = ErrorMessage;
            }
        }
        catch (Exception e)
        {
            HasError = true;
            ErrorMessage = "获取插件详情失败：" + MessageOf(e);
            StatusMessage = ErrorMessage;
        }
        finally
        {
            IsLoading = false;
            LoadingVisibility = Visibility.Collapsed;
            UpdateViewState();
        }
    }

    /// <summary>下载并安装插件（/api/pluginstore/install，含依赖安装）。确认弹窗由页面负责。</summary>
    [RelayCommand]
    private async Task InstallAsync()
    {
        if (IsInstalling || string.IsNullOrEmpty(_pluginId))
        {
            return;
        }

        IsInstalling = true;
        StatusMessage = "正在安装插件，请稍后…";
        try
        {
            await _service.InstallPluginAsync(_pluginId).ConfigureAwait(true);
            StatusMessage = $"插件 {Name} 安装完成（部分插件可能需要重启才会彻底生效）";
        }
        catch (Exception e)
        {
            StatusMessage = "插件安装失败：" + MessageOf(e);
        }
        finally
        {
            IsInstalling = false;
        }
    }

    private void Fill(EntityPlugin plugin)
    {
        Name = plugin.Name ?? "（未命名插件）";
        _pluginId = plugin.Id ?? _pluginId;
        PluginId = _pluginId;
        Publisher = plugin.Publisher ?? string.Empty;
        PublishDate = plugin.PublishDate ?? string.Empty;
        Version = plugin.Version ?? string.Empty;
        DownloadCount = (plugin.DownloadCount ?? 0).ToString();
        ShortDescription = plugin.ShortDescription ?? string.Empty;
        DetailDescription = plugin.DetailDescription ?? string.Empty;
        LogoUrl = plugin.LogoUrl ?? string.Empty;
        HasLogo = !string.IsNullOrWhiteSpace(LogoUrl);

        Dependencies.Clear();
        if (plugin.Dependencies != null)
        {
            foreach (var dependency in plugin.Dependencies)
            {
                Dependencies.Add(new PluginDependencyViewModel(dependency.Id, dependency.Name));
            }
        }

        DependenciesVisibility = Dependencies.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        DependenciesHint = Dependencies.Count > 0 ? string.Empty : "无依赖";

        // 对应 Vue confirmMessage（无依赖时不列依赖）
        ConfirmMessage = Dependencies.Count > 0
            ? $"即将安装 {Name} 插件，并安装 {string.Join("、", Dependencies.Select(d => d.Name))} 依赖，确认进行安装吗？"
            : $"即将安装 {Name} 插件，确认进行安装吗？";
    }

    private void UpdateViewState()
    {
        ContentVisibility = !HasError && !IsLoading ? Visibility.Visible : Visibility.Collapsed;
        ErrorVisibility = HasError ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string MessageOf(Exception e)
        => e is AggregateException aggregate && aggregate.InnerException != null
            ? aggregate.InnerException.Message
            : e.Message;
}
