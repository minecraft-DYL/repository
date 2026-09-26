using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lanw.App.Services;

namespace Lanw.App.ViewModels;

/// <summary>
/// 插件商城页视图模型（对应原 Vue plugin/PluginStore.vue + PluginsShopController 的 /api/pluginstore/get）。
/// 数据链路：PluginService → Lanw.Public.Message.PlugInstoreMessage.GetPluginList（进程内直调，无 HTTP）。
/// 支持搜索过滤（对应 Vue computed filteredPlugins：名称/简介/发布者，不区分大小写）、
/// 分页加载更多、以及行内安装（PlugInstoreMessage.Install，含依赖安装）。
/// </summary>
public sealed partial class PluginStoreViewModel : ObservableObject
{
    /// <summary>每页数量（PlugInstoreMessage.GetPluginList 默认 limit = 10）。</summary>
    public const int PageSize = 10;

    private readonly PluginService _service;
    private readonly List<PluginStoreItemViewModel> _all = [];

    /// <summary>当前展示的插件（已按搜索词过滤）。</summary>
    public ObservableCollection<PluginStoreItemViewModel> Plugins { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    [ObservableProperty]
    public partial string ResultSummary { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingMore { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial bool CanLoadMore { get; set; }

    [ObservableProperty]
    public partial Visibility LoadingVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility ListVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility ErrorVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility EmptyVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility LoadMoreVisibility { get; set; }

    public PluginStoreViewModel(PluginService? service = null)
    {
        _service = service ?? new PluginService();
        SearchText = string.Empty;
        StatusMessage = "尚未加载插件商城";
        ResultSummary = string.Empty;
        ErrorMessage = string.Empty;
        LoadingVisibility = Visibility.Collapsed;
        ListVisibility = Visibility.Collapsed;
        ErrorVisibility = Visibility.Collapsed;
        EmptyVisibility = Visibility.Collapsed;
        LoadMoreVisibility = Visibility.Collapsed;
    }

    /// <summary>加载首屏商城列表（也是错误态「重试」入口）。</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;
        LoadingVisibility = Visibility.Visible;
        ErrorVisibility = Visibility.Collapsed;
        EmptyVisibility = Visibility.Collapsed;
        StatusMessage = "正在获取插件商城列表…";
        try
        {
            var items = await _service.GetStoreListAsync(0, PageSize).ConfigureAwait(true);
            _all.Clear();
            foreach (var item in items)
            {
                _all.Add(new PluginStoreItemViewModel(item, InstallItemCommand));
            }

            CanLoadMore = items.Length >= PageSize;
            ApplyFilter();
            StatusMessage = items.Length == 0 ? "插件商城没有返回数据" : $"已加载 {_all.Count} 个插件";
        }
        catch (Exception e)
        {
            // 无网络 / 接口异常：进入错误态展示，不向 UI 线程抛出
            _all.Clear();
            Plugins.Clear();
            CanLoadMore = false;
            HasError = true;
            ErrorMessage = "无法获取插件商城列表：" + MessageOf(e);
            StatusMessage = ErrorMessage;
        }
        finally
        {
            IsLoading = false;
            LoadingVisibility = Visibility.Collapsed;
            UpdateViewState();
        }
    }

    /// <summary>加载下一页（追加，按 Id 去重）。</summary>
    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsLoading || IsLoadingMore || !CanLoadMore)
        {
            return;
        }

        IsLoadingMore = true;
        StatusMessage = "正在加载更多插件…";
        try
        {
            var items = await _service.GetStoreListAsync(_all.Count, PageSize).ConfigureAwait(true);
            foreach (var item in items)
            {
                if (item.Id != null && _all.Any(existing => existing.Id == item.Id))
                {
                    continue;
                }

                _all.Add(new PluginStoreItemViewModel(item, InstallItemCommand));
            }

            CanLoadMore = items.Length >= PageSize;
            ApplyFilter();
            StatusMessage = $"已加载 {_all.Count} 个插件";
        }
        catch (Exception e)
        {
            CanLoadMore = false;
            StatusMessage = "加载更多失败：" + MessageOf(e);
        }
        finally
        {
            IsLoadingMore = false;
            UpdateViewState();
        }
    }

    /// <summary>安装插件（/api/pluginstore/install，PlugInstoreMessage.Install 会一并安装依赖）。</summary>
    [RelayCommand]
    private async Task InstallItemAsync(PluginStoreItemViewModel? item)
    {
        if (item is null || item.IsInstalling || string.IsNullOrEmpty(item.Id))
        {
            return;
        }

        item.IsInstalling = true;
        StatusMessage = $"正在安装插件 {item.Name}（含依赖）…";
        try
        {
            await _service.InstallPluginAsync(item.Id).ConfigureAwait(true);
            StatusMessage = $"插件 {item.Name} 安装完成（部分插件需重启才彻底生效）";
        }
        catch (Exception e)
        {
            StatusMessage = $"插件 {item.Name} 安装失败：{MessageOf(e)}";
        }
        finally
        {
            item.IsInstalling = false;
        }
    }

    /// <summary>搜索词变化即重新过滤（对应 Vue computed filteredPlugins）。</summary>
    partial void OnSearchTextChanged(string value) => ApplyFilter();

    /// <summary>按搜索词过滤：匹配名称、简介或发布者（不区分大小写）。</summary>
    private void ApplyFilter()
    {
        var keyword = SearchText?.Trim() ?? string.Empty;
        var filtered = keyword.Length == 0
            ? _all
            : _all.Where(item =>
                item.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || item.ShortDescription.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || item.Publisher.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

        Plugins.Clear();
        foreach (var item in filtered)
        {
            Plugins.Add(item);
        }

        ResultSummary = keyword.Length == 0 ? $"共 {Plugins.Count} 个插件" : $"匹配 {Plugins.Count} 个插件";
        UpdateViewState();
    }

    private void UpdateViewState()
    {
        ListVisibility = Plugins.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ErrorVisibility = HasError ? Visibility.Visible : Visibility.Collapsed;
        EmptyVisibility = !HasError && !IsLoading && Plugins.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        LoadMoreVisibility = !HasError && CanLoadMore && Plugins.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        if (!HasError && !IsLoading && Plugins.Count == 0)
        {
            StatusMessage = _all.Count == 0 ? "插件商城没有返回数据" : "没有匹配的插件";
        }
    }

    private static string MessageOf(Exception e)
        => e is AggregateException aggregate && aggregate.InnerException != null
            ? aggregate.InnerException.Message
            : e.Message;
}
