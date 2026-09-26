using Lanw.Core;
using Lanw.Core.Entities.Login;
using Lanw.Core.Manager;
using Lanw.Core.Utils;
using Lanw.Core.Utils.CodeTools;
using Lanw.Game.Launcher.Entities;
using Lanw.Game.Launcher.Entities.WPFLauncher.Minecraft;
using Lanw.Game.Launcher.Entities.WPFLauncher.NetGame.GameLaunch.Texture;
using Lanw.Game.Launcher.Services.Java;
using Lanw.Game.Launcher.Utils;
using Serilog;

namespace Lanw.App.Services;

/// <summary>一次启动请求（对应原 Vue GameLaunchManager / 后端 launch 接口的入参口径）。</summary>
public sealed record GameLaunchRequest
{
    /// <summary>游戏版本（EnumGameVersion 枚举数值，原样交给 Lanw.Game.Launcher）。</summary>
    public int GameVersionId { get; init; }

    /// <summary>服务器 ID（为空表示本地启动）。</summary>
    public string ServerId { get; init; } = string.Empty;

    /// <summary>角色名（游戏内昵称）。</summary>
    public string RoleName { get; init; } = string.Empty;

    /// <summary>服务器地址（可空）。</summary>
    public string ServerIp { get; init; } = string.Empty;

    /// <summary>服务器端口（可空）。</summary>
    public int ServerPort { get; init; }

    /// <summary>是否安装白端核心模组。</summary>
    public bool LoadCoreMods { get; init; } = true;
}

/// <summary>已启动的游戏实例（对应原 Vue 列表的 role_name / game_name / user_id / game_version / 关闭）。</summary>
public sealed class RunningGameInstance
{
    public required int Id { get; init; }

    public required string RoleName { get; init; }

    public required string GameName { get; init; }

    public required string UserId { get; init; }

    public required string GameVersion { get; init; }

    public required LauncherService Launcher { get; init; }

    /// <summary>进程 ID（未拉起时为 -1）。</summary>
    public int ProcessId => Launcher.GetPid();

    /// <summary>是否仍在运行。</summary>
    public bool IsRunning => Launcher.IsRunning();
}

/// <summary>
/// 启动管理服务（进程内直调 Lanw.Game.Launcher，无 HTTP）：
/// 组装 EntityLaunchGame → LauncherService.LaunchGameAsync，并维护「运行中实例」列表
/// （对应原 Vue GameLaunchManager.vue 的实例列表 / 关闭 / 关闭全部）。
/// 进度以 EntityProgressUpdate 形态上报（下载/解压/启动阶段），日志走 Serilog（LaunchLogSink 汇聚到页面）。
/// </summary>
public sealed class GameLaunchService
{
    private static readonly Lock SyncLock = new();

    private readonly List<RunningGameInstance> _instances = [];
    private int _nextId = 1;

    /// <summary>应用内单例（离开页面再回来仍能看到运行中的实例）。</summary>
    public static GameLaunchService Instance { get; } = new();

    /// <summary>运行中的实例快照（倒序：最新启动在前）。</summary>
    public IReadOnlyList<RunningGameInstance> Instances
    {
        get
        {
            lock (SyncLock)
            {
                return _instances.ToArray();
            }
        }
    }

    /// <summary>启动游戏（失败抛异常，由调用方转为界面提示，不崩溃）。</summary>
    public async Task<RunningGameInstance> LaunchAsync(
        GameLaunchRequest request,
        IProgress<EntityProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // ① 校验参数与账号
        Report(progress, 5, "校验启动参数与账号…");
        if (string.IsNullOrWhiteSpace(request.RoleName))
        {
            throw new InvalidOperationException("请先填写角色名（游戏内昵称）。");
        }

        var account = ResolveAccount();

        // ② 环境检查（Java 运行时；缺失不阻断，交由启动流程报错并提示）
        Report(progress, 15, "检查 Java 运行时与游戏目录…");
        WarnIfJavaMissing(request.GameVersionId);

        var entity = BuildEntity(request, account);
        var launcher = new LauncherService(entity);

        // ③ 安装/校验游戏文件（下载 + 解压，最耗时的阶段）
        Report(progress, 35, "安装/校验游戏文件（下载与解压耗时较长，请稍候）…");
        Log.Information("开始启动游戏：版本 {0}，角色 {1}，服务器 {2}", entity.GameVersion, entity.RoleName, string.IsNullOrWhiteSpace(entity.GameId) ? "(本地)" : entity.GameId);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await launcher.LaunchGameAsync().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log.Error("启动失败：{0}", MessageOf(e));
            Report(progress, 100, "启动失败");
            throw;
        }

        // ④ 拉起进程 / 运行中
        var instance = new RunningGameInstance
        {
            Id = NextId(),
            RoleName = entity.RoleName,
            GameName = string.IsNullOrWhiteSpace(entity.GameId) ? "本地启动" : entity.GameId,
            UserId = entity.Account.UserId ?? string.Empty,
            GameVersion = entity.GameVersion,
            Launcher = launcher,
        };

        lock (SyncLock)
        {
            _instances.Insert(0, instance);
        }

        var pid = launcher.GetPid();
        if (pid > 0)
        {
            Report(progress, 100, $"游戏进程已启动（PID {pid}）");
            Log.Information("游戏启动成功：PID {0}，角色 {1}", pid, entity.RoleName);
        }
        else
        {
            Report(progress, 100, "启动流程已完成，但未检测到游戏进程");
            Log.Warning("启动流程完成但未取得游戏进程（PID -1），角色 {0}", entity.RoleName);
        }

        return instance;
    }

    /// <summary>关闭单个实例（对应 Vue closeGameLaunch）。</summary>
    public bool Close(int id)
    {
        RunningGameInstance? instance;
        lock (SyncLock)
        {
            instance = _instances.FirstOrDefault(item => item.Id == id);
            if (instance != null)
            {
                _instances.Remove(instance);
            }
        }

        if (instance is null)
        {
            return false;
        }

        SafeShutdown(instance);
        return true;
    }

    /// <summary>关闭全部实例（对应 Vue closeGameLaunch 批量关闭）。</summary>
    public int CloseAll()
    {
        RunningGameInstance[] snapshot;
        lock (SyncLock)
        {
            snapshot = _instances.ToArray();
            _instances.Clear();
        }

        foreach (var instance in snapshot)
        {
            SafeShutdown(instance);
        }

        return snapshot.Length;
    }

    /// <summary>剔除已退出的实例（进程自然结束 / 关闭失败）。</summary>
    public int PruneExited()
    {
        List<RunningGameInstance> exited;
        lock (SyncLock)
        {
            exited = _instances.Where(instance => !instance.IsRunning).ToList();
            foreach (var instance in exited)
            {
                _instances.Remove(instance);
            }
        }

        return exited.Count;
    }

    /// <summary>读取当前游戏账号；未登录时回落到本地保存的涅槃登录态（account/token）。</summary>
    private static EntityUserInfo ResolveAccount()
    {
        try
        {
            return InfoManager.GetGameAccount();
        }
        catch (ErrorCodeException)
        {
            var account = LanwConfig.GetValue("account", () => string.Empty);
            var token = LanwConfig.GetValue("token", () => string.Empty);
            if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("尚未登录账号，请先在「登录」或「账号管理」页登录后再启动游戏。");
            }

            return new EntityUserInfo
            {
                UserId = account,
                Token = token,
            };
        }
    }

    private static EntityLaunchGame BuildEntity(GameLaunchRequest request, EntityUserInfo account)
    {
        var version = GameVersionConverter.Convert(request.GameVersionId);
        var versionName = string.IsNullOrWhiteSpace(GameVersionUtil.GetGameVersionFromEnum(version))
            ? request.GameVersionId.ToString()
            : GameVersionUtil.GetGameVersionFromEnum(version);

        return new EntityLaunchGame
        {
            Id = 0,
            Account = account,
            GameId = request.ServerId?.Trim() ?? string.Empty,
            RoleName = request.RoleName.Trim(),
            GameVersionId = request.GameVersionId,
            GameVersion = versionName,
            GameName = string.IsNullOrWhiteSpace(request.ServerId) ? "本地启动" : request.ServerId.Trim(),
            ClientType = EnumGameClientType.Java,
            GameType = string.IsNullOrWhiteSpace(request.ServerId) ? EnumGType.SingleGame : EnumGType.NetGame,
            ServerIp = request.ServerIp?.Trim() ?? string.Empty,
            ServerPort = request.ServerPort,
            LoadCoreMods = request.LoadCoreMods,
        };
    }

    /// <summary>Java 运行时缺失只提示（无 Java 环境时不崩溃，由启动流程抛出可读错误）。</summary>
    private static void WarnIfJavaMissing(int gameVersionId)
    {
        try
        {
            var version = GameVersionConverter.Convert(gameVersionId);
            var javaHome = version switch
            {
                >= EnumGameVersion.V_1_20_6 => PathUtil.Jre21Path,
                >= EnumGameVersion.V_1_16 => PathUtil.Jre17Path,
                _ => PathUtil.Jre8Path,
            };

            var javaExe = Path.Combine(javaHome, "bin", PathUtil.JavaExePath);
            if (!File.Exists(javaExe))
            {
                Log.Warning("未检测到 Java 运行时：{0}（游戏文件修复/安装后仍缺失时启动会失败）", javaExe);
            }
            else
            {
                Log.Information("Java 运行时：{0}", javaExe);
            }
        }
        catch (Exception e)
        {
            Log.Warning("检查 Java 运行时失败：{0}", e.Message);
        }
    }

    private static void SafeShutdown(RunningGameInstance instance)
    {
        try
        {
            instance.Launcher.ShutdownAsync().GetAwaiter().GetResult();
            Log.Information("已关闭游戏实例：{0}（角色 {1}）", instance.Id, instance.RoleName);
        }
        catch (Exception e)
        {
            Log.Warning("关闭游戏实例 {0} 失败：{1}", instance.Id, e.Message);
        }
    }

    private int NextId()
    {
        lock (SyncLock)
        {
            return _nextId++;
        }
    }

    private static void Report(IProgress<EntityProgressUpdate>? progress, int percent, string message)
    {
        progress?.Report(new EntityProgressUpdate
        {
            Id = Guid.NewGuid(),
            Percent = percent,
            Message = message,
        });
    }

    private static string MessageOf(Exception e)
        => e is AggregateException aggregate && aggregate.InnerException != null
            ? MessageOf(aggregate.InnerException)
            : e.Message;
}
