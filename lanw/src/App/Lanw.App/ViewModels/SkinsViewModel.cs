using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lanw.App.Services;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameLaunch.Texture;

namespace Lanw.App.ViewModels;

/// <summary>
/// 皮肤页视图模型（对应原 Vue skin/Skins.vue）。
/// 数据链路：SkinService → Lanw.Public.SkinMessage / Lanw.WPFLauncher.NPFLauncher（进程内直调，无 HTTP）。
/// 功能：
/// - 皮肤列表（名称/预览图/简介/开发者/下载量/点赞数）分批加载（每页 15，累计上限 150，对应 Vue 的 offset&gt;=150 终止条件）；
/// - 按名称搜索（切换为搜索结果集，清空搜索词回到列表集）；
/// - 上传本地皮肤（Texture 协议：PNG 选择器 → SkinService.UploadLocalSkinAsync → 可选应用）；
/// - 无网络/未登录/接口异常时进入错误态，不抛出、不崩溃。
/// </summary>
public sealed partial class SkinsViewModel : ObservableObject
{
    private readonly SkinService _service;
    private readonly AuthService _auth;
    private readonly List<SkinItemViewModel> _listItems = [];
    private readonly List<SkinItemViewModel> _searchItems = [];

    /// <summary>当前展示的皮肤（搜索结果优先，与 Vue 的 filteredSkins 口径一致）。</summary>
    public ObservableCollection<SkinItemViewModel> Skins { get; } = [];

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
    public partial bool IsSearching { get; set; }

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

    [ObservableProperty]
    public partial Visibility SearchHintVisibility { get; set; }

    // ---- 上传本地皮肤（Texture 协议） ----

    [ObservableProperty]
    public partial string UploadStatusMessage { get; set; }

    [ObservableProperty]
    public partial Visibility UploadStatusVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility UploadErrorVisibility { get; set; }

    [ObservableProperty]
    public partial bool IsUploading { get; set; }

    /// <summary>上传按钮可用性（上传进行中禁用，避免重复提交）。</summary>
    [ObservableProperty]
    public partial bool CanUpload { get; set; }

    /// <summary>上传成功后返回的用户游戏贴图（Texture 协议 EntityUserGameTexture）。</summary>
    [ObservableProperty]
    public partial EntityUserGameTexture? UploadedTexture { get; set; }

    [ObservableProperty]
    public partial string UploadedInfo { get; set; }

    [ObservableProperty]
    public partial Visibility UploadedInfoVisibility { get; set; }

    public SkinsViewModel(SkinService? service = null, AuthService? auth = null)
    {
        _service = service ?? new SkinService();
        _auth = auth ?? new AuthService();
        SearchText = string.Empty;
        StatusMessage = "尚未加载皮肤列表";
        ResultSummary = string.Empty;
        ErrorMessage = string.Empty;
        UploadStatusMessage = string.Empty;
        UploadedInfo = string.Empty;
        LoadingVisibility = Visibility.Collapsed;
        ListVisibility = Visibility.Collapsed;
        ErrorVisibility = Visibility.Collapsed;
        EmptyVisibility = Visibility.Collapsed;
        LoadMoreVisibility = Visibility.Collapsed;
        SearchHintVisibility = Visibility.Collapsed;
        UploadStatusVisibility = Visibility.Collapsed;
        UploadErrorVisibility = Visibility.Collapsed;
        UploadedInfoVisibility = Visibility.Collapsed;
        CanUpload = true;
    }

    /// <summary>上传进行中禁用按钮。</summary>
    partial void OnIsUploadingChanged(bool value) => CanUpload = !value;

    /// <summary>登录状态提示（应用/上传需要已登录账号；未登录时给出提示而不发请求）。</summary>
    public bool IsLoggedIn
    {
        get
        {
            try
            {
                return _auth.IsLoggedIn;
            }
            catch (Exception)
            {
                return false; // 本地账号文件异常时按未登录处理，页面给出提示即可
            }
        }
    }

    /// <summary>加载首屏皮肤列表（也是错误态「重试」入口）。</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        HasError = false;
        IsSearching = false;
        ErrorMessage = string.Empty;
        LoadingVisibility = Visibility.Visible;
        ErrorVisibility = Visibility.Collapsed;
        EmptyVisibility = Visibility.Collapsed;
        StatusMessage = "正在获取皮肤列表…";
        try
        {
            var items = await _service.GetSkinListAsync(0, SkinService.PageSize).ConfigureAwait(true);
            _listItems.Clear();
            foreach (var item in items)
            {
                _listItems.Add(new SkinItemViewModel(item));
            }

            _searchItems.Clear();
            CanLoadMore = items.Length >= SkinService.PageSize && _listItems.Count < SkinService.MaxListItems;
            ApplyFilter();
            StatusMessage = items.Length == 0 ? "没有获取到皮肤数据" : $"已加载 {_listItems.Count} 个皮肤";
        }
        catch (Exception e)
        {
            // 无网络 / 接口异常：进入错误态展示，不向 UI 线程抛出（避免启动器崩溃）
            _listItems.Clear();
            _searchItems.Clear();
            Skins.Clear();
            CanLoadMore = false;
            HasError = true;
            ErrorMessage = "无法获取皮肤列表：" + MessageOf(e) + LoginHintOf(e);
            StatusMessage = ErrorMessage;
        }
        finally
        {
            IsLoading = false;
            LoadingVisibility = Visibility.Collapsed;
            UpdateViewState();
        }
    }

    /// <summary>加载下一页（追加，按 EntityId 去重；列表集与搜索结果集各自分页）。</summary>
    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsLoading || IsLoadingMore || !CanLoadMore)
        {
            return;
        }

        IsLoadingMore = true;
        StatusMessage = "正在加载更多皮肤…";
        try
        {
            if (IsSearching)
            {
                var keyword = SearchText?.Trim() ?? string.Empty;
                var more = await _service.SearchSkinAsync(keyword, _searchItems.Count, SkinService.PageSize).ConfigureAwait(true);
                AppendDistinct(_searchItems, more);
                CanLoadMore = more.Length >= SkinService.PageSize && _searchItems.Count < SkinService.MaxListItems;
                StatusMessage = $"已加载 {_searchItems.Count} 个匹配皮肤";
            }
            else
            {
                var more = await _service.GetSkinListAsync(_listItems.Count, SkinService.PageSize).ConfigureAwait(true);
                AppendDistinct(_listItems, more);
                CanLoadMore = more.Length >= SkinService.PageSize && _listItems.Count < SkinService.MaxListItems;
                StatusMessage = $"已加载 {_listItems.Count} 个皮肤";
            }
        }
        catch (Exception e)
        {
            CanLoadMore = false;
            StatusMessage = "加载更多失败：" + MessageOf(e);
        }
        finally
        {
            IsLoadingMore = false;
            ApplyFilter();
        }
    }

    /// <summary>按名称搜索（对应原 Vue getGameSkinListByName；空关键词则回到列表集）。</summary>
    [RelayCommand]
    private async Task SearchAsync()
    {
        var keyword = SearchText?.Trim() ?? string.Empty;
        if (keyword.Length == 0)
        {
            IsSearching = false;
            _searchItems.Clear();
            ApplyFilter();
            StatusMessage = $"已加载 {_listItems.Count} 个皮肤";
            return;
        }

        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        HasError = false;
        IsSearching = true;
        ErrorMessage = string.Empty;
        LoadingVisibility = Visibility.Visible;
        ErrorVisibility = Visibility.Collapsed;
        StatusMessage = $"正在搜索「{keyword}」…";
        try
        {
            var items = await _service.SearchSkinAsync(keyword, 0, SkinService.PageSize).ConfigureAwait(true);
            _searchItems.Clear();
            AppendDistinct(_searchItems, items);
            CanLoadMore = items.Length >= SkinService.PageSize && _searchItems.Count < SkinService.MaxListItems;
            StatusMessage = _searchItems.Count == 0 ? $"没有匹配「{keyword}」的皮肤" : $"匹配 {_searchItems.Count} 个皮肤";
        }
        catch (Exception e)
        {
            _searchItems.Clear();
            CanLoadMore = false;
            HasError = true;
            ErrorMessage = "搜索皮肤失败：" + MessageOf(e) + LoginHintOf(e);
            StatusMessage = ErrorMessage;
        }
        finally
        {
            IsLoading = false;
            LoadingVisibility = Visibility.Collapsed;
            ApplyFilter();
        }
    }

    /// <summary>搜索词被清空时立即回到列表集（对应 Vue watch(searchQuery) 的重置行为）。</summary>
    partial void OnSearchTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value) && IsSearching && !IsLoading)
        {
            IsSearching = false;
            _searchItems.Clear();
            ApplyFilter();
            StatusMessage = $"已加载 {_listItems.Count} 个皮肤";
            return;
        }

        SearchHintVisibility = !IsSearching && !string.IsNullOrWhiteSpace(value)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// 上传本地皮肤（Texture 协议）：由页面选择 PNG 后调用。
    /// 未登录时直接给出提示（应用/上传都需要账号令牌），上传失败进入上传错误提示，不抛出。
    /// </summary>
    /// <param name="pngBytes">皮肤 PNG 字节</param>
    /// <param name="fileName">文件名（含扩展名）</param>
    public async Task UploadLocalSkinAsync(byte[] pngBytes, string fileName)
    {
        if (IsUploading)
        {
            return;
        }

        UploadedTexture = null;
        UploadedInfoVisibility = Visibility.Collapsed;
        UploadErrorVisibility = Visibility.Collapsed;
        UploadStatusVisibility = Visibility.Visible;

        if (!SkinFilePicker.IsPng(pngBytes))
        {
            UploadStatusMessage = "所选文件不是有效的 PNG 图片（皮肤必须是 .png）";
            UploadErrorVisibility = Visibility.Visible;
            return;
        }

        if (!IsLoggedIn)
        {
            UploadStatusMessage = "上传皮肤需要先登录账号：请到「登录」页登录后重试。";
            UploadErrorVisibility = Visibility.Visible;
            return;
        }

        IsUploading = true;
        UploadStatusMessage = $"正在上传「{fileName}」…";
        try
        {
            var texture = await _service.UploadLocalSkinAsync(pngBytes, fileName).ConfigureAwait(true);
            UploadedTexture = texture;
            UploadedInfo = $"上传成功：skin_id={Display(texture.SkinId)} / entity_id={Display(texture.EntityId)}"
                + $" / game_type={texture.GameType} / skin_type={texture.SkinType} / skin_mode={texture.SkinMode}";
            UploadedInfoVisibility = Visibility.Visible;
            UploadStatusMessage = "上传成功。点击「应用上传的皮肤」可写入各游戏类型。";
        }
        catch (Exception e)
        {
            UploadStatusMessage = "上传本地皮肤失败：" + MessageOf(e);
            UploadErrorVisibility = Visibility.Visible;
        }
        finally
        {
            IsUploading = false;
            ApplyUploadedCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// 展示文件选择阶段的提示（未选中/校验不通过）：仅提示，不进入上传流程。
    /// </summary>
    /// <param name="message">提示文本</param>
    /// <param name="isError">true=按错误样式展示；false=普通提示（如用户取消）</param>
    public void ShowPickMessage(string message, bool isError)
    {
        UploadStatusVisibility = Visibility.Visible;
        UploadStatusMessage = message;
        UploadErrorVisibility = isError ? Visibility.Visible : Visibility.Collapsed;
        UploadedInfoVisibility = Visibility.Collapsed;
        UploadedTexture = null;
    }

    /// <summary>是否可应用刚上传的贴图（未上传或正在上传时不可用）。</summary>
    public bool CanApplyUploaded => UploadedTexture is not null && !IsUploading;

    /// <summary>把刚上传的贴图应用到各游戏类型（SetSkinAsync）。</summary>
    [RelayCommand(CanExecute = nameof(CanApplyUploaded))]
    private async Task ApplyUploadedAsync()
    {
        var texture = UploadedTexture;
        if (texture is null)
        {
            return;
        }

        var skinId = string.IsNullOrWhiteSpace(texture.SkinId) ? texture.EntityId : texture.SkinId;
        if (!IsLoggedIn)
        {
            UploadStatusMessage = "应用皮肤需要先登录账号：请到「登录」页登录后重试。";
            UploadErrorVisibility = Visibility.Visible;
            UploadStatusVisibility = Visibility.Visible;
            return;
        }

        UploadErrorVisibility = Visibility.Collapsed;
        UploadStatusVisibility = Visibility.Visible;
        UploadStatusMessage = "正在应用皮肤…";
        try
        {
            var response = await _service.ApplySkinAsync(skinId).ConfigureAwait(true);
            UploadStatusMessage = response is null
                ? "应用皮肤完成（接口未返回内容）。"
                : "应用皮肤完成：" + (string.IsNullOrWhiteSpace(response.Message) ? "已写入各游戏类型" : response.Message);
        }
        catch (Exception e)
        {
            UploadStatusMessage = "应用皮肤失败：" + MessageOf(e);
            UploadErrorVisibility = Visibility.Visible;
        }
    }

    /// <summary>把列表集或搜索结果集同步到界面（对应 Vue filteredSkins）。</summary>
    private void ApplyFilter()
    {
        var source = IsSearching ? _searchItems : _listItems;
        Skins.Clear();
        foreach (var item in source)
        {
            Skins.Add(item);
        }

        var keyword = SearchText?.Trim() ?? string.Empty;
        ResultSummary = IsSearching
            ? $"搜索「{keyword}」：{Skins.Count} 个结果"
            : $"共 {Skins.Count} 个皮肤（累计上限 {SkinService.MaxListItems}）";
        UpdateViewState();
    }

    /// <summary>追加去重（按 EntityId）。</summary>
    private static void AppendDistinct(List<SkinItemViewModel> target, IEnumerable<Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameSkin.EntityQueryNetSkinItem> items)
    {
        foreach (var item in items)
        {
            if (target.Any(existing => existing.EntityId == item.EntityId))
            {
                continue;
            }

            target.Add(new SkinItemViewModel(item));
        }
    }

    /// <summary>统一刷新各状态的可见性（列表 / 空态 / 错误态 / 加载更多）。</summary>
    private void UpdateViewState()
    {
        ListVisibility = Skins.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ErrorVisibility = HasError ? Visibility.Visible : Visibility.Collapsed;
        EmptyVisibility = !HasError && !IsLoading && Skins.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        LoadMoreVisibility = !HasError && CanLoadMore && Skins.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        SearchHintVisibility = !IsSearching && !string.IsNullOrWhiteSpace(SearchText) && Skins.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (!HasError && !IsLoading && Skins.Count == 0)
        {
            StatusMessage = IsSearching
                ? $"没有匹配「{SearchText?.Trim()}」的皮肤"
                : _listItems.Count == 0 ? "没有获取到皮肤数据" : "没有匹配的皮肤";
        }
    }

    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "(空)" : value;

    private static string MessageOf(Exception e)
        => e is AggregateException aggregate && aggregate.InnerException != null
            ? aggregate.InnerException.Message
            : e.Message;

    /// <summary>
    /// 接口因未登录返回时的可操作提示（皮肤列表/上传/应用均需账号令牌：
    /// X19Extensions.Gateway 按 TokenUtil 附带 user-id/user-token 签名头，未登录时接口回「没有登录」）。
    /// </summary>
    private static string LoginHintOf(Exception e)
        => MessageOf(e).Contains("登录", StringComparison.Ordinal)
            ? "（皮肤列表需要登录后的账号令牌：请到「登录」页登录后点「刷新」重试）"
            : string.Empty;
}
