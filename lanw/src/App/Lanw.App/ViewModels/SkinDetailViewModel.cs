using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lanw.App.Services;
using Microsoft.UI.Xaml.Media;

namespace Lanw.App.ViewModels;

/// <summary>
/// 皮肤详情页视图模型（对应原 Vue skin/SkinDetail.vue 的信息区）：
/// 大图预览 + 皮肤 ID + 作者/发布时间/下载量/点赞数 + 皮肤介绍，
/// 以及「应用设置」（EntitySkinSettings：skin_type=31 / client_type=java，写入单机/网络服/租赁服/本地联机/大厅）。
/// 数据链路：SkinService → EntitySkinDetail / NPFLauncher.SetSkinAsync（进程内直调，无 HTTP）；
/// 无网络时显示错误态并在页内重试，不崩溃。
/// </summary>
public sealed partial class SkinDetailViewModel : ObservableObject
{
    private readonly SkinService _service;
    private readonly AuthService _auth;

    [ObservableProperty]
    public partial string SkinId { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string EntityIdText { get; set; }

    [ObservableProperty]
    public partial string DeveloperName { get; set; }

    [ObservableProperty]
    public partial string PublishTime { get; set; }

    [ObservableProperty]
    public partial string DownloadNumText { get; set; }

    [ObservableProperty]
    public partial string LikeNumText { get; set; }

    [ObservableProperty]
    public partial string BriefSummary { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string ApplyMessage { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial bool IsApplying { get; set; }

    [ObservableProperty]
    public partial Visibility LoadingVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility ErrorVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility ContentVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility ApplyMessageVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility ApplyErrorVisibility { get; set; }

    [ObservableProperty]
    public partial ImageSource? MainImage { get; set; }

    [ObservableProperty]
    public partial Visibility PlaceholderVisibility { get; set; }

    /// <summary>应用皮肤时会写入的游戏类型（与 NPFLauncher.SetSkinAsync 内的 EntitySkinSettings 一致）。</summary>
    public IReadOnlyList<string> AppliedGameTypes => SkinService.AppliedGameTypes;

    /// <summary>应用设置的固定字段（对应 EntitySkinSettings 的常量部分）。</summary>
    public string SettingsSummary => "skin_type=31、client_type=java、skin_mode=0";

    public SkinDetailViewModel(SkinService? service = null, AuthService? auth = null)
    {
        _service = service ?? new SkinService();
        _auth = auth ?? new AuthService();
        SkinId = string.Empty;
        Name = string.Empty;
        EntityIdText = string.Empty;
        DeveloperName = string.Empty;
        PublishTime = string.Empty;
        DownloadNumText = string.Empty;
        LikeNumText = string.Empty;
        BriefSummary = string.Empty;
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;
        ApplyMessage = string.Empty;
        LoadingVisibility = Visibility.Collapsed;
        ErrorVisibility = Visibility.Collapsed;
        ContentVisibility = Visibility.Collapsed;
        ApplyMessageVisibility = Visibility.Collapsed;
        ApplyErrorVisibility = Visibility.Collapsed;
        PlaceholderVisibility = Visibility.Visible;
    }

    /// <summary>登录状态提示（应用皮肤需要已登录账号）。</summary>
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
                return false;
            }
        }
    }

    /// <summary>加载皮肤详情（也是错误态「重试」入口；参数为皮肤 ID）。</summary>
    [RelayCommand]
    private async Task LoadAsync(string? skinId)
    {
        SkinId = skinId ?? SkinId;
        if (string.IsNullOrWhiteSpace(SkinId) || IsLoading)
        {
            return;
        }

        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;
        LoadingVisibility = Visibility.Visible;
        ErrorVisibility = Visibility.Collapsed;
        ContentVisibility = Visibility.Collapsed;
        StatusMessage = "正在获取皮肤详情…";
        try
        {
            var detail = await _service.GetSkinDetailAsync(SkinId).ConfigureAwait(true);
            Name = string.IsNullOrWhiteSpace(detail.Name) ? "(未命名皮肤)" : detail.Name;
            EntityIdText = $"皮肤ID: {detail.EntityId}";
            DeveloperName = string.IsNullOrWhiteSpace(detail.DeveloperName) ? "未知" : detail.DeveloperName;
            PublishTime = string.IsNullOrWhiteSpace(detail.PublishTime) ? "未知" : detail.PublishTime;
            DownloadNumText = (detail.DownloadNum ?? 0).ToString();
            LikeNumText = (detail.LikeNum ?? 0).ToString();
            BriefSummary = string.IsNullOrWhiteSpace(detail.BriefSummary) ? "该作者没有填写皮肤介绍。" : detail.BriefSummary;
            MainImage = SkinImageLoader.Create(detail.TitleImageUrl);
            PlaceholderVisibility = MainImage is null ? Visibility.Visible : Visibility.Collapsed;
            ContentVisibility = Visibility.Visible;
            StatusMessage = $"已加载皮肤详情（{Name}）";
        }
        catch (Exception e)
        {
            // 无网络 / 接口异常 / ID 非法（ErrorCode.IdError）：进入错误态，不向 UI 线程抛出
            HasError = true;
            ErrorMessage = "无法获取皮肤详情：" + MessageOf(e);
            StatusMessage = ErrorMessage;
            MainImage = null;
            PlaceholderVisibility = Visibility.Visible;
        }
        finally
        {
            IsLoading = false;
            LoadingVisibility = Visibility.Collapsed;
            ErrorVisibility = HasError ? Visibility.Visible : Visibility.Collapsed;
            ContentVisibility = HasError ? Visibility.Collapsed : ContentVisibility;
        }
    }

    /// <summary>应用皮肤（对应原 Vue setGameSkin → NPFLauncher.SetSkinAsync）。</summary>
    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (IsApplying || string.IsNullOrWhiteSpace(SkinId))
        {
            return;
        }

        ApplyMessageVisibility = Visibility.Visible;
        ApplyErrorVisibility = Visibility.Collapsed;

        if (!IsLoggedIn)
        {
            ApplyMessage = "应用皮肤需要先登录账号：请到「登录」页登录后重试。";
            ApplyErrorVisibility = Visibility.Visible;
            return;
        }

        IsApplying = true;
        ApplyMessage = "正在设置皮肤中，请稍后…";
        try
        {
            var response = await _service.ApplySkinAsync(SkinId).ConfigureAwait(true);
            ApplyMessage = response is null
                ? "皮肤应用完成（接口未返回内容）。"
                : "皮肤应用完成：" + (string.IsNullOrWhiteSpace(response.Message) ? "已写入各游戏类型" : response.Message);
        }
        catch (Exception e)
        {
            ApplyMessage = "应用皮肤失败：" + MessageOf(e);
            ApplyErrorVisibility = Visibility.Visible;
        }
        finally
        {
            IsApplying = false;
        }
    }

    private static string MessageOf(Exception e)
        => e is AggregateException aggregate && aggregate.InnerException != null
            ? aggregate.InnerException.Message
            : e.Message;
}
