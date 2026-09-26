using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lanw.App.Models;
using Lanw.App.Services;
using Lanw.Core;
using Lanw.Game.Launcher.Entities;
using Lanw.Game.Launcher.Utils;
using Microsoft.UI.Dispatching;
using Serilog;

namespace Lanw.App.ViewModels;

/// <summary>
/// 启动管理页视图模型：版本选择 + 游戏内存 + JVM 参数（LanwConfig 持久化）+ 一键启动，
/// 并在界面上展示启动进度（EntityProgressUpdate：下载/解压/启动阶段）与启动日志，
/// 下方维护「运行中的游戏实例」列表（对应原 Vue GameLaunchManager.vue 的列表 / 关闭 / 关闭全部）。
/// 数据链路：进程内直调 Lanw.Game.Launcher（GameLaunchService），无 HTTP。
/// </summary>
public sealed partial class GameLaunchManagerViewModel : ObservableObject
{
    /// <summary>服务器 ID 持久化键（本页新增，字符串键避免与已有数值键类型冲突）。</summary>
    private const string KeyServerId = "launchServerId";

    /// <summary>角色名持久化键。</summary>
    private const string KeyRoleName = "launchRoleName";

    /// <summary>上次选择的版本持久化键。</summary>
    private const string KeyVersionId = "launchVersionId";

    /// <summary>缺省版本：1.18（enum 数值）。</summary>
    private const int DefaultVersionId = 1018000;

    /// <summary>日志区最多保留行数。</summary>
    private const int MaxLogLines = 500;

    private readonly GameLaunchService _service;
    private readonly LaunchLogSink _logSink = LaunchLogSink.Instance;
    private readonly DispatcherQueue? _dispatcher = DispatcherQueue.GetForCurrentThread();

    private bool _logSubscribed;

    /// <summary>可选游戏版本（枚举数值升序倒排：新版本在前）。</summary>
    public ObservableCollection<GameVersionOption> Versions { get; } = [];

    /// <summary>运行中的游戏实例。</summary>
    public ObservableCollection<GameInstanceItemViewModel> Instances { get; } = [];

    /// <summary>启动日志（最新在末尾）。</summary>
    public ObservableCollection<string> LogLines { get; } = [];

    [ObservableProperty]
    public partial GameVersionOption? SelectedVersion { get; set; }

    /// <summary>游戏内存 MB（LanwConfig.gameMemory）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MemoryDisplay))]
    public partial double GameMemory { get; set; }

    /// <summary>JVM 参数（LanwConfig.jvmArgs，启动器 CommandService 直接读取）。</summary>
    [ObservableProperty]
    public partial string JvmArgs { get; set; }

    /// <summary>服务器 ID（为空 = 本地启动）。</summary>
    [ObservableProperty]
    public partial string ServerId { get; set; }

    /// <summary>角色名（游戏内昵称，必填）。</summary>
    [ObservableProperty]
    public partial string RoleName { get; set; }

    /// <summary>服务器地址（可空，仅记录到启动实体）。</summary>
    [ObservableProperty]
    public partial string ServerIp { get; set; }

    /// <summary>服务器端口（可空）。</summary>
    [ObservableProperty]
    public partial string ServerPort { get; set; }

    /// <summary>是否安装白端核心模组。</summary>
    [ObservableProperty]
    public partial bool LoadCoreMods { get; set; }

    /// <summary>当前游戏账号展示。</summary>
    [ObservableProperty]
    public partial string AccountDisplay { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    public partial bool IsLaunching { get; set; }

    [ObservableProperty]
    public partial double ProgressPercent { get; set; }

    [ObservableProperty]
    public partial string ProgressMessage { get; set; }

    [ObservableProperty]
    public partial Visibility ProgressVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility InstancesVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility EmptyVisibility { get; set; }

    [ObservableProperty]
    public partial string InstanceSummary { get; set; }

    /// <summary>游戏内存展示文本。</summary>
    public string MemoryDisplay => $"{(int)GameMemory} MB（{GameMemory / 1024:F1} GB）";

    public GameLaunchManagerViewModel(GameLaunchService? service = null)
    {
        _service = service ?? GameLaunchService.Instance;

        SelectedVersion = null;
        GameMemory = 4096;
        JvmArgs = string.Empty;
        ServerId = string.Empty;
        RoleName = string.Empty;
        ServerIp = string.Empty;
        ServerPort = string.Empty;
        LoadCoreMods = true;
        AccountDisplay = "未登录";
        StatusMessage = "尚未启动游戏";
        ProgressPercent = 0;
        ProgressMessage = string.Empty;
        ProgressVisibility = Visibility.Collapsed;
        InstancesVisibility = Visibility.Collapsed;
        EmptyVisibility = Visibility.Visible;
        InstanceSummary = "当前没有运行的游戏实例";

        BuildVersions();
        LoadParameters();
    }

    /// <summary>页面进入：订阅日志、刷新账号与实例列表。</summary>
    public void Activate()
    {
        if (!_logSubscribed)
        {
            // 先把本页的 Sink 挂到 Serilog 全局 Logger 上（幂等）：启动器模块（Lanw.Game.Launcher）
            // 的 Log.* 输出（下载/解压/安装/启动失败）才会进到本页日志区。
            LaunchLogSink.Attach();
            _logSink.LogEmitted += OnLogEmitted;
            _logSubscribed = true;
            _logSink.Replay(AppendLog);
        }

        RefreshAccount();
        RefreshInstances();
    }

    /// <summary>页面离开：取消订阅（游戏实例继续运行，不随页面关闭）。</summary>
    public void Deactivate()
    {
        if (!_logSubscribed)
        {
            return;
        }

        _logSink.LogEmitted -= OnLogEmitted;
        _logSubscribed = false;
    }

    /// <summary>刷新当前游戏账号（登录/账号管理页操作后回到本页可见）。</summary>
    [RelayCommand]
    private void RefreshAccount()
    {
        try
        {
            var account = Lanw.Core.Manager.InfoManager.GetGameAccount();
            AccountDisplay = $"{account.UserId}（{account.Account}）";
        }
        catch (Exception)
        {
            AccountDisplay = "未登录（请先登录账号）";
        }
    }

    /// <summary>刷新运行中实例列表。</summary>
    [RelayCommand]
    private void RefreshInstances()
    {
        _service.PruneExited();

        Instances.Clear();
        foreach (var instance in _service.Instances)
        {
            Instances.Add(new GameInstanceItemViewModel(
                instance.Id.ToString(),
                instance.RoleName,
                instance.GameName,
                instance.UserId,
                instance.GameVersion,
                instance.IsRunning ? $"运行中（PID {instance.ProcessId}）" : "已结束")
            {
                InstanceId = instance.Id,
            });
        }

        var hasInstances = Instances.Count > 0;
        InstancesVisibility = hasInstances ? Visibility.Visible : Visibility.Collapsed;
        EmptyVisibility = hasInstances ? Visibility.Collapsed : Visibility.Visible;
        InstanceSummary = hasInstances
            ? $"当前运行的游戏实例数量：{Instances.Count}"
            : "当前没有运行的游戏实例";
    }

    /// <summary>保存启动参数（内存 / JVM 参数 / 上次启动目标）到 LanwConfig。</summary>
    [RelayCommand]
    private void SaveParameters()
    {
        try
        {
            if (GameMemory < 1024)
            {
                StatusMessage = "游戏内存不能小于 1024 MB";
                return;
            }

            LanwConfig.SetGameMemory(((int)GameMemory).ToString());
            LanwConfig.SetValue("jvmArgs", JvmArgs ?? string.Empty);
            EnsureStringKey(KeyServerId, string.Empty);
            EnsureStringKey(KeyRoleName, string.Empty);
            EnsureStringKey(KeyVersionId, DefaultVersionId.ToString());
            LanwConfig.SetValue(KeyServerId, ServerId ?? string.Empty);
            LanwConfig.SetValue(KeyRoleName, RoleName ?? string.Empty);
            LanwConfig.SetValue(KeyVersionId, (SelectedVersion?.VersionId ?? DefaultVersionId).ToString());
            StatusMessage = "启动参数已保存（重启应用后仍然生效）";
        }
        catch (Exception e)
        {
            StatusMessage = $"保存启动参数失败：{MessageOf(e)}";
        }
    }

    /// <summary>清空日志区。</summary>
    [RelayCommand]
    private void ClearLog()
    {
        LogLines.Clear();
        StatusMessage = "已清空启动日志";
    }

    /// <summary>关闭指定游戏实例（对应 Vue closeGameLaunch）。</summary>
    [RelayCommand]
    private void CloseInstance(GameInstanceItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            StatusMessage = _service.Close(item.InstanceId)
                ? $"已关闭游戏实例：{item.RoleName}"
                : $"实例已不存在：{item.RoleName}";
        }
        catch (Exception e)
        {
            StatusMessage = $"关闭游戏实例失败：{MessageOf(e)}";
        }
        finally
        {
            RefreshInstances();
        }
    }

    /// <summary>关闭全部游戏实例（对应 Vue closeGameLaunch 批量关闭）。</summary>
    [RelayCommand]
    private void CloseAll()
    {
        try
        {
            var count = _service.CloseAll();
            StatusMessage = count > 0 ? $"已关闭全部 {count} 个游戏实例" : "当前没有运行的游戏实例";
        }
        catch (Exception e)
        {
            StatusMessage = $"关闭全部游戏实例失败：{MessageOf(e)}";
        }
        finally
        {
            RefreshInstances();
        }
    }

    /// <summary>启动游戏（无 Java 环境 / 未登录 / 网络异常时给出可读提示，不崩溃）。</summary>
    [RelayCommand(CanExecute = nameof(CanLaunch))]
    private async Task LaunchAsync()
    {
        if (IsLaunching)
        {
            return;
        }

        SaveParameters();

        IsLaunching = true;
        ProgressVisibility = Visibility.Visible;
        ProgressPercent = 0;
        ProgressMessage = "准备启动…";
        StatusMessage = "正在启动游戏…";

        try
        {
            var request = new GameLaunchRequest
            {
                GameVersionId = SelectedVersion?.VersionId ?? DefaultVersionId,
                ServerId = ServerId ?? string.Empty,
                RoleName = RoleName ?? string.Empty,
                ServerIp = ServerIp ?? string.Empty,
                ServerPort = ParsePort(ServerPort),
                LoadCoreMods = LoadCoreMods,
            };

            // Progress<T> 在 UI 线程创建，回调自动切回 UI 线程
            var progress = new Progress<EntityProgressUpdate>(OnProgress);

            // 启动流程包含同步阻塞的安装/解压步骤，放到线程池执行，避免界面卡死
            var instance = await Task.Run(() => _service.LaunchAsync(request, progress)).ConfigureAwait(true);

            StatusMessage = instance.IsRunning
                ? $"启动成功：角色 {instance.RoleName}（PID {instance.ProcessId}）"
                : $"启动流程已完成：角色 {instance.RoleName}";
        }
        catch (Exception e)
        {
            // 无 Java 运行时 / 未登录 / 下载失败：只提示，不向 UI 线程抛出
            ProgressPercent = 100;
            ProgressMessage = "启动失败";
            StatusMessage = "启动失败：" + MessageOf(e);
            AppendLog($"[启动失败] {MessageOf(e)}");
        }
        finally
        {
            IsLaunching = false;
            RefreshInstances();
        }
    }

    private bool CanLaunch() => !IsLaunching;

    private void OnProgress(EntityProgressUpdate update)
    {
        if (update is null)
        {
            return;
        }

        // 进度只前进不后退（并发上报时避免回跳）
        ProgressPercent = Math.Max(ProgressPercent, Math.Clamp(update.Percent, 0, 100));
        ProgressMessage = string.IsNullOrWhiteSpace(update.Message) ? ProgressMessage : update.Message;
    }

    /// <summary>日志回调（来自任意线程，统一切回 UI 线程）。</summary>
    private void OnLogEmitted(string line)
    {
        if (_dispatcher is null || _dispatcher.HasThreadAccess)
        {
            AppendLog(line);
            return;
        }

        _dispatcher.TryEnqueue(() => AppendLog(line));
    }

    private void AppendLog(string line)
    {
        LogLines.Add(line);
        while (LogLines.Count > MaxLogLines)
        {
            LogLines.RemoveAt(0);
        }

        if (IsLaunching)
        {
            AdviseStageFromLog(line);
        }
    }

    /// <summary>
    /// 依据启动器日志关键词推进阶段进度（下载 / 解压 / 安装 / 启动），
    /// 让界面上能看到「下载→解压→启动」的推进，而不是一条静止的进度条。
    /// </summary>
    private void AdviseStageFromLog(string line)
    {
        var (percent, message) = DetectStage(line);
        if (percent <= 0 || percent <= ProgressPercent)
        {
            return;
        }

        ProgressPercent = percent;
        if (!string.IsNullOrWhiteSpace(message))
        {
            ProgressMessage = message;
        }
    }

    private static (int Percent, string Message) DetectStage(string line)
    {
        if (Contains(line, "Downloading", "下载"))
        {
            return (50, "正在下载游戏文件…");
        }

        if (Contains(line, "Extracting", "解压"))
        {
            return (65, "正在解压游戏文件…");
        }

        if (Contains(line, "Installed", "安装完成"))
        {
            return (75, "游戏文件安装完成…");
        }

        if (Contains(line, "launched successfully", "启动成功"))
        {
            return (95, "游戏进程已拉起…");
        }

        return (0, string.Empty);
    }

    private static bool Contains(string source, string first, string second)
        => source.Contains(first, StringComparison.OrdinalIgnoreCase)
           || source.Contains(second, StringComparison.Ordinal);

    private void BuildVersions()
    {
        try
        {
            var options = Enum.GetValues<EnumGameVersion>()
                .Where(version => version != EnumGameVersion.NONE)
                .Where(version => !version.ToString().Contains("CPP", StringComparison.OrdinalIgnoreCase))
                .Where(version => !version.ToString().Contains("RTX", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(version => (uint)version)
                .Select(version =>
                {
                    var name = GameVersionUtil.GetGameVersionFromEnum(version);
                    return new GameVersionOption((int)(uint)version, string.IsNullOrWhiteSpace(name) ? version.ToString() : name);
                })
                .ToList();

            Versions.Clear();
            foreach (var option in options)
            {
                Versions.Add(option);
            }
        }
        catch (Exception e)
        {
            Log.Warning("构建游戏版本列表失败：{0}", e.Message);
        }
    }

    private void LoadParameters()
    {
        try
        {
            GameMemory = LanwConfig.GetValue<int>("gameMemory");
            JvmArgs = LanwConfig.GetValue<string>("jvmArgs");
        }
        catch (Exception e)
        {
            StatusMessage = $"载入启动参数失败：{MessageOf(e)}";
        }

        try
        {
            EnsureStringKey(KeyServerId, string.Empty);
            EnsureStringKey(KeyRoleName, string.Empty);
            EnsureStringKey(KeyVersionId, DefaultVersionId.ToString());
            ServerId = LoadString(KeyServerId);
            RoleName = LoadString(KeyRoleName);
            var versionText = LoadString(KeyVersionId, DefaultVersionId.ToString());
            var versionId = int.TryParse(versionText, out var parsed) ? parsed : DefaultVersionId;
            SelectedVersion = Versions.FirstOrDefault(option => option.VersionId == versionId) ?? Versions.FirstOrDefault();
        }
        catch (Exception e)
        {
            Log.Warning("载入上次启动目标失败：{0}", e.Message);
            SelectedVersion ??= Versions.FirstOrDefault();
        }
    }

    /// <summary>
    /// 读取字符串配置。键不存在时按类型注册默认值；已存在（json 中已保存过）时直接读取。
    /// 注意：不能对已存在的键调用 AddByTypeName —— 它内部走 SetDefaultFrom → SetFrom，
    /// 会把已保存的值重置为默认值（曾导致「重启后角色名/版本丢失」）。
    /// </summary>
    private static string LoadString(string key, string fallback = "")
    {
        try
        {
            return LanwConfig.GetValue<string>(key);
        }
        catch (KeyNotFoundException)
        {
            LanwConfig.AddByTypeName(key, fallback, "string");
            return LanwConfig.GetValue<string>(key);
        }
    }

    /// <summary>确保配置键已注册（仅注册，不覆盖已有值）。</summary>
    private static void EnsureStringKey(string key, string fallback)
    {
        try
        {
            _ = LanwConfig.GetValue<string>(key);
        }
        catch (KeyNotFoundException)
        {
            LanwConfig.AddByTypeName(key, fallback, "string");
        }
    }

    private static int ParsePort(string? text)
        => int.TryParse(text, out var port) && port > 0 && port <= 65535 ? port : 0;

    private static string MessageOf(Exception e)
        => e is AggregateException aggregate && aggregate.InnerException != null
            ? MessageOf(aggregate.InnerException)
            : e.Message;
}
