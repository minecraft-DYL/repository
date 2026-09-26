using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lanw.App.Services;

namespace Lanw.App.ViewModels;

/// <summary>
/// 租赁服页视图模型（对应原 Vue rental/GameRental.vue）。
/// 数据链路：RentalGameService → Lanw.Public.RentalGameMessage / Lanw.WPFLauncher 租赁服协议（进程内直调，无 HTTP）。
/// 支持搜索过滤（对应 Vue 的 searchQuery）、分批加载更多（对应 Vue 的 loadServersInBatches 口径，
/// 桌面端改为显式「加载更多」按钮，避免后台死循环），以及无网络时的错误态（不抛出、不崩溃）。
/// </summary>
public sealed partial class GameRentalViewModel : ObservableObject
{
    /// <summary>每页数量（对应原 Vue loadMoreServers(15)）。</summary>
    public const int PageSize = 15;

    private readonly RentalGameService _service;
    private readonly List<RentalGameItemViewModel> _all = [];

    /// <summary>当前展示的租赁服（已按搜索词过滤）。</summary>
    public ObservableCollection<RentalGameItemViewModel> Servers { get; } = [];

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

    public GameRentalViewModel(RentalGameService? service = null)
    {
        _service = service ?? new RentalGameService();
        SearchText = string.Empty;
        StatusMessage = "尚未加载租赁服列表";
        ResultSummary = string.Empty;
        ErrorMessage = string.Empty;
        LoadingVisibility = Visibility.Collapsed;
        ListVisibility = Visibility.Collapsed;
        ErrorVisibility = Visibility.Collapsed;
        EmptyVisibility = Visibility.Collapsed;
        LoadMoreVisibility = Visibility.Collapsed;
    }

    /// <summary>加载首屏租赁服列表（也是错误态「重试」入口）。</summary>
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
        StatusMessage = "正在获取租赁服列表…";
        try
        {
            var entries = await _service.GetServerListAsync(0, PageSize).ConfigureAwait(true);
            _all.Clear();
            foreach (var entry in entries)
            {
                _all.Add(new RentalGameItemViewModel(entry));
            }

            CanLoadMore = entries.Length >= PageSize;
            ApplyFilter();
            StatusMessage = entries.Length == 0 ? "没有获取到租赁服数据" : $"已加载 {_all.Count} 个租赁服";
        }
        catch (Exception e)
        {
            // 无网络 / 接口异常：进入错误态展示，不向 UI 线程抛出（避免启动器崩溃）
            _all.Clear();
            Servers.Clear();
            CanLoadMore = false;
            HasError = true;
            ErrorMessage = "无法获取租赁服列表：" + MessageOf(e);
            StatusMessage = ErrorMessage;
        }
        finally
        {
            IsLoading = false;
            LoadingVisibility = Visibility.Collapsed;
            UpdateViewState();
        }
    }

    /// <summary>加载下一页（追加，按 EntityId 去重）。</summary>
    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsLoading || IsLoadingMore || !CanLoadMore)
        {
            return;
        }

        IsLoadingMore = true;
        StatusMessage = "正在加载更多租赁服…";
        try
        {
            var entries = await _service.GetServerListAsync(_all.Count, PageSize).ConfigureAwait(true);
            foreach (var entry in entries)
            {
                if (_all.Any(existing => existing.EntityId == entry.EntityId))
                {
                    continue;
                }

                _all.Add(new RentalGameItemViewModel(entry));
            }

            CanLoadMore = entries.Length >= PageSize;
            ApplyFilter();
            StatusMessage = $"已加载 {_all.Count} 个租赁服";
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

    /// <summary>搜索词变化即重新过滤（对应 Vue computed filteredServers）。</summary>
    partial void OnSearchTextChanged(string value) => ApplyFilter();

    /// <summary>按搜索词过滤：匹配名称或简介（不区分大小写，对应 Vue 的 server_name / brief_summary 口径）。</summary>
    private void ApplyFilter()
    {
        var keyword = SearchText?.Trim() ?? string.Empty;
        var filtered = keyword.Length == 0
            ? _all
            : _all.Where(item =>
                item.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || item.BriefSummary.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

        Servers.Clear();
        foreach (var item in filtered)
        {
            Servers.Add(item);
        }

        ResultSummary = keyword.Length == 0 ? $"共 {Servers.Count} 个租赁服" : $"匹配 {Servers.Count} 个租赁服";
        UpdateViewState();
    }

    /// <summary>统一刷新各状态的可见性（列表 / 空态 / 错误态 / 加载更多）。</summary>
    private void UpdateViewState()
    {
        ListVisibility = Servers.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ErrorVisibility = HasError ? Visibility.Visible : Visibility.Collapsed;
        EmptyVisibility = !HasError && !IsLoading && Servers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        LoadMoreVisibility = !HasError && CanLoadMore && Servers.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        if (!HasError && !IsLoading && Servers.Count == 0)
        {
            StatusMessage = _all.Count == 0 ? "没有获取到租赁服数据" : "没有匹配的租赁服";
        }
    }

    private static string MessageOf(Exception e)
        => e is AggregateException aggregate && aggregate.InnerException != null
            ? aggregate.InnerException.Message
            : e.Message;
}
