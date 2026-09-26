using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lanw.App.Services;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace Lanw.App.ViewModels;

/// <summary>
/// 服务器详情页视图模型（对应原 Vue netgame/ServerDetail.vue 的信息区）。
/// 数据链路：ServerService → Lanw.Public.EntityServerDetail（进程内直调：并行拉取详情 + 服务器地址）。
/// 展示：消息/信息列表（服务器 ID、作者、创建时间、游戏版本、服务器地址+一键复制）、
/// 主图 + 小图切换、服务器介绍；无网络时进入错误态（不抛出、不崩溃）。
/// 注：原 Vue 的 Launch / 启动代理弹窗属于启动流程（后续任务），本页不涉及。
/// </summary>
public sealed partial class ServerDetailViewModel : ObservableObject
{
    private readonly ServerService _service;

    /// <summary>小图列表（点击切换主图）。</summary>
    public ObservableCollection<ServerImageItemViewModel> Images { get; } = [];

    [ObservableProperty]
    public partial string ServerId { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string AuthorDisplay { get; set; }

    [ObservableProperty]
    public partial string CreatedAtDisplay { get; set; }

    [ObservableProperty]
    public partial string GameVersionDisplay { get; set; }

    [ObservableProperty]
    public partial string Address { get; set; }

    [ObservableProperty]
    public partial string AddressDisplay { get; set; }

    [ObservableProperty]
    public partial string FullDescription { get; set; }

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
    public partial Visibility ImagesVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility LoadingVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility ErrorVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility ContentVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility AddressActionsVisibility { get; set; }

    // ---- 服务器 MOD（需求变更：启动游戏不移植，改为点进服务器即说明该服 JAVA 游戏版本与 mod，并可导出 ZIP）----

    /// <summary>该服 MOD 资源包清单（一次 GetNetGameComponentDownloadListAsync 获取）。</summary>
    public ObservableCollection<ServerModEntry> Mods { get; } = [];

    /// <summary>该服所需 Java 版本（网易下发，如 Java 17）。</summary>
    [ObservableProperty]
    public partial string JavaVersionDisplay { get; set; }

    /// <summary>MOD 资源包对应的 MC 版本名。</summary>
    [ObservableProperty]
    public partial string McVersionDisplay { get; set; }

    /// <summary>MOD 区状态文案（加载中 / 数量 / 导出进度 / 失败原因）。</summary>
    [ObservableProperty]
    public partial string ModsSummary { get; set; }

    [ObservableProperty]
    public partial Visibility ModsVisibility { get; set; }

    /// <summary>是否正在导出 ZIP（用于按钮文案与并发保护）。</summary>
    [ObservableProperty]
    public partial bool IsExporting { get; set; }

    /// <summary>视图注入：弹出「保存 ZIP 位置」选择器（入参为建议文件名，用户取消返回 null）。</summary>
    public Func<string, Task<string?>>? SaveZipPrompt { get; set; }

    public ServerDetailViewModel(ServerService? service = null)
    {
        _service = service ?? new ServerService();
        ServerId = string.Empty;
        Name = string.Empty;
        AuthorDisplay = string.Empty;
        CreatedAtDisplay = string.Empty;
        GameVersionDisplay = string.Empty;
        Address = string.Empty;
        AddressDisplay = string.Empty;
        FullDescription = string.Empty;
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;
        MainImagePlaceholderVisibility = Visibility.Visible;
        ImagesVisibility = Visibility.Collapsed;
        LoadingVisibility = Visibility.Collapsed;
        ErrorVisibility = Visibility.Collapsed;
        ContentVisibility = Visibility.Collapsed;
        AddressActionsVisibility = Visibility.Collapsed;
        JavaVersionDisplay = "未获取";
        McVersionDisplay = "未获取";
        ModsSummary = string.Empty;
        ModsVisibility = Visibility.Collapsed;
    }

    /// <summary>进入详情页时由页面调用：记录服务器 ID 并加载详情（也是错误态「重试」入口）。</summary>
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
        StatusMessage = "正在获取服务器详情…";
        try
        {
            var detail = await _service.GetServerDetailAsync(id).ConfigureAwait(true);
            Apply(detail);
            StatusMessage = "服务器详情已加载";

            // MOD / Java 版本单独加载：该接口需要已登录的游戏账号，失败也不影响详情区展示
            _ = LoadModsAsync(id);
        }
        catch (Exception e)
        {
            // 无网络 / 接口异常：进入错误态展示，不向 UI 线程抛出（避免启动器崩溃）
            Images.Clear();
            MainImage = null;
            MainImagePlaceholderVisibility = Visibility.Visible;
            ImagesVisibility = Visibility.Collapsed;
            ContentVisibility = Visibility.Collapsed;
            AddressActionsVisibility = Visibility.Collapsed;
            HasError = true;
            ErrorMessage = "无法获取服务器详情：" + MessageOf(e);
            StatusMessage = ErrorMessage;
        }
        finally
        {
            IsLoading = false;
            LoadingVisibility = Visibility.Collapsed;
            ErrorVisibility = HasError ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>加载该服 MOD / Java 版本元数据（对应 GetNetGameComponentDownloadListAsync）。</summary>
    private async Task LoadModsAsync(string serverId)
    {
        ModsSummary = "正在获取服务器 MOD 信息…";
        ModsVisibility = Visibility.Collapsed;
        try
        {
            var info = await ServerModService.GetAsync(serverId).ConfigureAwait(true);

            JavaVersionDisplay = info.JavaVersionText;
            McVersionDisplay = string.IsNullOrWhiteSpace(info.McVersionName) ? "未知" : info.McVersionName;

            Mods.Clear();
            foreach (var mod in info.Mods)
            {
                Mods.Add(mod);
            }

            ModsSummary = Mods.Count == 0
                ? "该服务器未下发 MOD 资源包。"
                : $"共 {Mods.Count} 个 MOD 资源包，可导出为 ZIP。";
            ModsVisibility = Visibility.Visible;
        }
        catch (Exception e)
        {
            Mods.Clear();
            JavaVersionDisplay = "未获取";
            McVersionDisplay = "未获取";
            ModsSummary = "无法获取服务器 MOD 信息（该接口需要已登录的游戏账号）：" + MessageOf(e);
            ModsVisibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// 导出 MOD 为 ZIP：先经 SaveZipPrompt 选保存位置，再逐个下载资源包、解压并收集 .jar 打包。
    /// </summary>
    [RelayCommand]
    private async Task ExportModsZipAsync()
    {
        if (IsExporting)
        {
            return;
        }

        if (Mods.Count == 0)
        {
            ModsSummary = "没有可导出的 MOD。";
            return;
        }

        if (SaveZipPrompt is null)
        {
            ModsSummary = "当前环境无法弹出保存位置选择器。";
            return;
        }

        var target = await SaveZipPrompt($"server-mods-{SanitizeFileName(ServerId)}").ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(target))
        {
            ModsSummary = "已取消导出。";
            return;
        }

        IsExporting = true;
        try
        {
            var info = new ServerModInfo(JavaVersionDisplay, McVersionDisplay, [.. Mods]);
            var (jarCount, zipPath) = await ModsZipExporter
                .ExportAsync(info, target, message => ModsSummary = message)
                .ConfigureAwait(true);

            ModsSummary = jarCount > 0
                ? $"已导出 {jarCount} 个 MOD 到：{zipPath}"
                : $"ZIP 已生成，但资源包内未找到 .jar：{zipPath}";
        }
        catch (Exception e)
        {
            ModsSummary = "导出 MOD 失败：" + MessageOf(e);
        }
        finally
        {
            IsExporting = false;
        }
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(value) ? "server" : value;
    }

    /// <summary>点击小图切换主图（对应 Vue switchImage）。</summary>
    public void ShowImage(ServerImageItemViewModel? image)
    {
        if (image is null)
        {
            return;
        }

        MainImage = image.Image;
        MainImagePlaceholderVisibility = image.Image is null ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>一键复制连接地址（对应详情页的地址复制需求）。</summary>
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

    private void Apply(Lanw.Public.Entities.NEL.EntityServerDetail detail)
    {
        Name = string.IsNullOrWhiteSpace(detail.Name) ? "(未命名服务器)" : detail.Name!;
        AuthorDisplay = string.IsNullOrWhiteSpace(detail.Author) ? "未知" : detail.Author!;
        CreatedAtDisplay = string.IsNullOrWhiteSpace(detail.CreatedAt) ? "未知" : detail.CreatedAt!;
        GameVersionDisplay = string.IsNullOrWhiteSpace(detail.GameVersion) ? "未知" : detail.GameVersion!;
        FullDescription = string.IsNullOrWhiteSpace(detail.FullDescription) ? "暂无服务器介绍" : detail.FullDescription!;

        Address = detail.Address ?? string.Empty;
        AddressDisplay = string.IsNullOrWhiteSpace(Address) ? "未获取到连接地址" : Address;
        AddressActionsVisibility = string.IsNullOrWhiteSpace(Address) ? Visibility.Collapsed : Visibility.Visible;

        Images.Clear();
        foreach (var url in detail.BriefImageUrls ?? [])
        {
            var image = ServerImageLoader.Create(url);
            if (image is null)
            {
                continue; // 无法解析（空地址 / 本地缓存文件缺失）的小图不展示，避免破图
            }

            Images.Add(new ServerImageItemViewModel(url, image));
        }

        MainImage = Images.FirstOrDefault()?.Image;
        MainImagePlaceholderVisibility = MainImage is null ? Visibility.Visible : Visibility.Collapsed;
        ImagesVisibility = Images.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ContentVisibility = Visibility.Visible;
    }

    private static string MessageOf(Exception e)
        => e is AggregateException aggregate && aggregate.InnerException != null
            ? aggregate.InnerException.Message
            : e.Message;
}
