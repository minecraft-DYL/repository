using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lanw.Core;

namespace Lanw.App.ViewModels;

/// <summary>
/// 设置页视图模型：读写 LanwConfig 中的本地设置（对应原 Vue Settings.vue 的四组配置：
/// 自动登录 / Chat | IRC / 启动配置 / 其它配置）。配置持久化在 resources/nirvanaAccount.json。
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    /// <summary>隐藏账号（hideAccount）。</summary>
    [ObservableProperty]
    public partial bool HideAccount { get; set; }

    /// <summary>聊天（IRC）开关（chatEnable）。</summary>
    [ObservableProperty]
    public partial bool ChatEnable { get; set; }

    /// <summary>游戏内存 MB（gameMemory）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MemoryDisplay))]
    public partial double GameMemory { get; set; }

    /// <summary>虚拟机参数（jvmArgs）。</summary>
    [ObservableProperty]
    public partial string JvmArgs { get; set; }

    /// <summary>游戏参数（gameArgs）。</summary>
    [ObservableProperty]
    public partial string GameArgs { get; set; }

    /// <summary>主动登录游戏（autoLoginGame）。</summary>
    [ObservableProperty]
    public partial bool AutoLoginGame { get; set; }

    /// <summary>主动登录 163Email（autoLoginGame163Email）。</summary>
    [ObservableProperty]
    public partial bool AutoLoginGame163Email { get; set; }

    /// <summary>主动登录 Cookie（autoLoginGameCookie）。</summary>
    [ObservableProperty]
    public partial bool AutoLoginGameCookie { get; set; }

    /// <summary>使用 javaw.exe（useJavaW，仅 Windows）。</summary>
    [ObservableProperty]
    public partial bool UseJavaW { get; set; }

    /// <summary>自动更新插件（autoUpdatePlugin）。</summary>
    [ObservableProperty]
    public partial bool AutoUpdatePlugin { get; set; }

    /// <summary>状态提示。</summary>
    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    /// <summary>游戏内存展示文本。</summary>
    public string MemoryDisplay => $"{(int)GameMemory} MB（{GameMemory / 1024:F1} GB）";

    /// <summary>默认虚拟机参数（与原版 LanwConfig 默认值一致）。</summary>
    private const string DefaultJvmArgs =
        "-XX:+UseG1GC -XX:-UseAdaptiveSizePolicy -XX:-OmitStackTraceInFastThrow " +
        "-Djdk.lang.Process.allowAmbiguousCommands=true -Dfml.ignoreInvalidMinecraftCertificates=True " +
        "-Dfml.ignorePatchDiscrepancies=True -Dlog4j2.formatMsgNoLookups=true";

    public SettingsViewModel()
    {
        JvmArgs = string.Empty;
        GameArgs = string.Empty;
        StatusMessage = string.Empty;
        LoadCore();
    }

    /// <summary>从 LanwConfig 载入当前设置。</summary>
    [RelayCommand]
    private void Load()
    {
        LoadCore();
        StatusMessage = "已载入当前设置";
    }

    /// <summary>保存设置（持久化到 resources/nirvanaAccount.json）。</summary>
    [RelayCommand]
    private void Save()
    {
        try
        {
            if (GameMemory < 1024)
            {
                StatusMessage = "游戏内存不能小于 1024 MB";
                return;
            }

            LanwConfig.SetValue("hideAccount", HideAccount);
            LanwConfig.SetValue("chatEnable", ChatEnable);
            LanwConfig.SetGameMemory(((int)GameMemory).ToString());
            LanwConfig.SetValue("jvmArgs", JvmArgs ?? string.Empty);
            LanwConfig.SetValue("gameArgs", GameArgs ?? string.Empty);
            LanwConfig.SetValue("autoLoginGame", AutoLoginGame);
            LanwConfig.SetValue("autoLoginGame163Email", AutoLoginGame163Email);
            LanwConfig.SetValue("autoLoginGameCookie", AutoLoginGameCookie);
            LanwConfig.SetValue("useJavaW", UseJavaW);
            LanwConfig.SetValue("autoUpdatePlugin", AutoUpdatePlugin);
            StatusMessage = "已保存设置（重启应用后仍然生效）";
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存失败：{ex.Message}";
        }
    }

    /// <summary>恢复默认值（仅填入界面，需点“保存设置”后生效）。</summary>
    [RelayCommand]
    private void ResetToDefault()
    {
        HideAccount = true;
        ChatEnable = true;
        GameMemory = 4096;
        JvmArgs = DefaultJvmArgs;
        GameArgs = string.Empty;
        AutoLoginGame = true;
        AutoLoginGame163Email = false;
        AutoLoginGameCookie = true;
        UseJavaW = true;
        AutoUpdatePlugin = true;
        StatusMessage = "已恢复默认值，点击“保存设置”后生效";
    }

    private void LoadCore()
    {
        try
        {
            HideAccount = LanwConfig.GetValue<bool>("hideAccount");
            ChatEnable = LanwConfig.GetValue<bool>("chatEnable");
            GameMemory = LanwConfig.GetValue<int>("gameMemory");
            JvmArgs = LanwConfig.GetValue<string>("jvmArgs");
            GameArgs = LanwConfig.GetValue<string>("gameArgs");
            AutoLoginGame = LanwConfig.GetValue<bool>("autoLoginGame");
            AutoLoginGame163Email = LanwConfig.GetValue<bool>("autoLoginGame163Email");
            AutoLoginGameCookie = LanwConfig.GetValue<bool>("autoLoginGameCookie");
            UseJavaW = LanwConfig.GetValue<bool>("useJavaW");
            AutoUpdatePlugin = LanwConfig.GetValue<bool>("autoUpdatePlugin");
        }
        catch (Exception ex)
        {
            StatusMessage = $"载入设置失败：{ex.Message}";
        }
    }
}
