using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lanw.Core.Utils;
using Lanw.Public.Message;
using Windows.ApplicationModel.DataTransfer;

namespace Lanw.App.ViewModels;

/// <summary>
/// 主页视图模型（对应原 Vue Home.vue + /api/home 数据）。
/// 公告卡（ad1/ad2/ad3：名称+文字）、版本信息（gameVersion/crcSalt）、随机名生成。
/// 数据链路：HomeMessage（内嵌服务器 /api/home 等价）→ InfoManager.ServerInfo 缓存。
/// </summary>
public sealed partial class HomeViewModel : ObservableObject
{
    /// <summary>公告一：名称（对应 EntityInfo.Ad1.Name，缺省占位对齐原 Vue `name || '广告位 1'`）。</summary>
    [ObservableProperty]
    public partial string Ad1Title { get; set; }

    [ObservableProperty]
    public partial string Ad1Text { get; set; }

    [ObservableProperty]
    public partial string Ad2Title { get; set; }

    [ObservableProperty]
    public partial string Ad2Text { get; set; }

    [ObservableProperty]
    public partial string Ad3Title { get; set; }

    [ObservableProperty]
    public partial string Ad3Text { get; set; }

    /// <summary>最新盒子版本号（X19.GameVersion：网易 patchlist 解析）。</summary>
    [ObservableProperty]
    public partial string GameVersion { get; set; }

    /// <summary>CRC 盐值（服务器下发）。</summary>
    [ObservableProperty]
    public partial string CrcSalt { get; set; }

    /// <summary>随机名生成结果。</summary>
    [ObservableProperty]
    public partial string RandomName { get; set; }

    /// <summary>页面状态提示（加载中/失败信息/复制反馈）。</summary>
    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    public HomeViewModel()
    {
        Ad1Title = "广告位 1";
        Ad1Text = "这里可以放置广告内容，展示您的产品或服务。";
        Ad2Title = "广告位 2";
        Ad2Text = "这里可以放置广告内容，展示您的产品或服务。";
        Ad3Title = "广告位 3";
        Ad3Text = "这里可以放置广告内容，展示您的产品或服务。";
        GameVersion = "获取中…";
        CrcSalt = "未下发";
        RandomName = "（点击下方按钮生成）";
        StatusMessage = string.Empty;
    }

    /// <summary>加载主页数据：公告/盐值（fantnel.json）+ 最新版本号（并行）。</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        StatusMessage = "正在连接服务器…";
        try
        {
            var infoTask = HomeMessage.GetHomeInfoAsync();
            var versionTask = HomeMessage.GetGameVersionAsync();
            var info = await infoTask.ConfigureAwait(true);

            // 公告卡（ad1/ad2/ad3：名称+文字，null 保留占位）
            if (info?.Ad1 != null)
            {
                Ad1Title = string.IsNullOrWhiteSpace(info.Ad1.Name) ? "广告位 1" : info.Ad1.Name;
                Ad1Text = string.IsNullOrWhiteSpace(info.Ad1.Text) ? "这里可以放置广告内容，展示您的产品或服务。" : info.Ad1.Text;
            }

            if (info?.Ad2 != null)
            {
                Ad2Title = string.IsNullOrWhiteSpace(info.Ad2.Name) ? "广告位 2" : info.Ad2.Name;
                Ad2Text = string.IsNullOrWhiteSpace(info.Ad2.Text) ? "这里可以放置广告内容，展示您的产品或服务。" : info.Ad2.Text;
            }

            if (info?.Ad3 != null)
            {
                Ad3Title = string.IsNullOrWhiteSpace(info.Ad3.Name) ? "广告位 3" : info.Ad3.Name;
                Ad3Text = string.IsNullOrWhiteSpace(info.Ad3.Text) ? "这里可以放置广告内容，展示您的产品或服务。" : info.Ad3.Text;
            }

            // CRC 盐值（可能随公告一起下发；也可由先前登录流程填充）
            var salt = Lanw.WPFLauncher.Protocol.X19.CrcSalt ?? info?.CrcSalt;
            CrcSalt = string.IsNullOrEmpty(salt) ? "未下发" : salt!;

            StatusMessage = info != null
                ? "公告与版本信息已更新"
                : "无法连接服务器，展示占位内容";

            // 最新盒子版本号（等待并行任务）
            var version = await versionTask.ConfigureAwait(true);
            GameVersion = version ?? "获取失败";
        }
        catch (Exception e)
        {
            StatusMessage = "加载失败：" + e.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>生成随机中文名（RandomNameUtil：前缀+后缀+数字，总长 7~9）。</summary>
    [RelayCommand]
    private void GenerateRandomName()
    {
        RandomName = RandomNameUtil.Generate();
        StatusMessage = "已生成随机名";
    }

    /// <summary>复制随机名到剪贴板。</summary>
    [RelayCommand]
    private void CopyRandomName()
    {
        var package = new DataPackage();
        package.SetText(RandomName);
        Clipboard.SetContent(package);
        StatusMessage = "已复制到剪贴板";
    }

    /// <summary>
    /// 导入主题包（对应原 Vue Home.vue 的 handleFile → setThemeSwitch → POST /api/theme/switch）：
    /// 解析 .fant.json 里的主题名 → 校验并应用主题 → 返回提示文本（原版用 Alert 弹窗展示 data.msg）。
    /// 解析失败提示与源一致：「Fantnel 主题 文件解析失败，请检查文件格式。」
    /// </summary>
    public async Task<(bool Ok, string Message)> ApplyThemeAsync(string? json)
    {
        var themeValue = ThemeMessage.ParseThemeValue(json);
        if (themeValue is null)
        {
            StatusMessage = "Fantnel 主题 文件解析失败，请检查文件格式。";
            return (false, StatusMessage);
        }

        try
        {
            await ThemeMessage.SwitchThemeAsync(themeValue).ConfigureAwait(true);
            StatusMessage = $"主题「{themeValue}」应用完成";
            return (true, StatusMessage);
        }
        catch (Exception e)
        {
            StatusMessage = "应用主题失败：" + e.Message;
            return (false, StatusMessage);
        }
    }
}
