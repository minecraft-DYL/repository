using Lanw.Core.Entities.Login;
using Lanw.Development;
using Lanw.Public.Entities.NEL;

namespace Lanw.App.Services;

/// <summary>
/// 运行中的代理条目：包装 t14 的 <see cref="RunningProxy"/>，并附带转发目标等展示信息
/// （RunningProxy 只保留 local_address/local_port/nick_name/server_name 四个 JSON 字段）。
/// </summary>
public sealed class RunningProxyEntry
{
    /// <summary>t14 的 RunningProxy（持有 Interceptor 的关闭句柄，Shutdown() 即关闭拦截器）。</summary>
    public required RunningProxy Proxy { get; init; }

    /// <summary>转发目标地址（生效后的 InterceptorConfig.ForwardAddress）。</summary>
    public required string ForwardAddress { get; init; }

    /// <summary>转发目标端口（生效后的 InterceptorConfig.ForwardPort）。</summary>
    public required int ForwardPort { get; init; }

    /// <summary>服务器版本（生效后的 InterceptorConfig.ServerVersion）。</summary>
    public required string ServerVersion { get; init; }

    /// <summary>游戏/服务器 ID（生效后的 InterceptorConfig.GameId）。</summary>
    public required string GameId { get; init; }

    /// <summary>启动时间。</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>代理 ID（RunningProxy.Id）。</summary>
    public int Id => Proxy.Id;

    /// <summary>游戏昵称。</summary>
    public string NickName => Proxy.GetNickName();

    /// <summary>本地监听地址。</summary>
    public string LocalAddress => Proxy.LocalAddress;

    /// <summary>本地监听端口。</summary>
    public int LocalPort => Proxy.LocalPort;

    /// <summary>服务器名称。</summary>
    public string ServerName => Proxy.ServerName;

    /// <summary>是否租凭服代理。</summary>
    public bool IsRental => Proxy.IsRental;
}

/// <summary>
/// 已启动代理的进程内注册表 + 启动入口（进程内直调 t20 的 Lanw.Development.Interceptor）。
///
/// 说明：参考实现把「已启动代理」状态放在 Nirvana.Public.Manager.ActiveGameAndProxies
/// （还同时管理白端 LauncherService），该管理器依赖尚未移植的 LauncherService/EntityLaunchGame
/// 与 RunningProxy(Interceptor) 强类型构造，属网络服/租凭服启动流程（t23/t24）范围，
/// 故本任务先在 App 层提供等价的代理注册表，接口与该管理器对齐（GetAllProxies/CloseProxy/CloseProxy(All)），
/// 待 ActiveGameAndProxies 移植完成后改为直接调用它。
/// </summary>
public sealed class ProxyService
{
    /// <summary>进程内单例（代理页与后续服务器页共用同一注册表）。</summary>
    public static ProxyService Current { get; } = new();

    private readonly Lock _lock = new();

    private readonly List<RunningProxyEntry> _proxies = [];

    // 与 _proxies 同序保存拦截器引用，用于关闭时释放监听/UDP 广播线程
    private readonly List<Interceptor> _interceptors = [];

    /// <summary>注册表变化通知（启动/关闭代理后触发，UI 据此刷新列表）。</summary>
    public event EventHandler? Changed;

    /// <summary>当前运行的代理数量。</summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _proxies.Count;
            }
        }
    }

    /// <summary>获取所有已启动代理（快照，等价于 ActiveGameAndProxies.GetAllProxies）。</summary>
    public IReadOnlyList<RunningProxyEntry> GetAllProxies()
    {
        lock (_lock)
        {
            return _proxies.ToArray();
        }
    }

    /// <summary>
    /// 启动本机脱盒拦截器并登记到注册表。
    /// 端口被占用时由 Interceptor.CreateInterceptor 内部（NetworkUtil.GetAvailablePort）自动向后取可用端口，
    /// 返回值中的 LocalPort/ForwardPort 均为实际生效值（取自 interceptor.CurrentConfig）。
    /// </summary>
    public RunningProxyEntry Start(ProxySettings settings, EntityAccount account)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(account);

        var interceptor = Interceptor.CreateInterceptor(
            settings.IsRental,
            settings.ModInfo,
            settings.GameId,
            settings.ServerName,
            settings.ServerVersion,
            settings.ForwardAddress,
            settings.ForwardPort,
            settings.NickName,
            account,
            // 会话服务器 ID 回调（Yggdrasil 认证）由网络服/租凭服启动流程注入，本页不伪造
            null,
            settings.LocalPort);

        var config = interceptor.CurrentConfig;
        var entry = new RunningProxyEntry
        {
            // 使用参考源同形的强类型构造 RunningProxy(Interceptor)（t14 已恢复）
            Proxy = new RunningProxy(interceptor)
            {
                Id = NextId(),
                Account = account,
                ServerId = config.GameId,
            },
            ForwardAddress = config.ForwardAddress,
            ForwardPort = config.ForwardPort,
            ServerVersion = config.ServerVersion,
            GameId = config.GameId,
            StartedAt = DateTimeOffset.Now,
        };

        lock (_lock)
        {
            _proxies.Add(entry);
            _interceptors.Add(interceptor);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return entry;
    }

    /// <summary>关闭单个代理（等价于 ActiveGameAndProxies.CloseProxy(int)）。</summary>
    public bool Close(int id)
    {
        RunningProxyEntry entry;
        lock (_lock)
        {
            var index = _proxies.FindIndex(item => item.Id == id);
            if (index < 0)
            {
                return false;
            }

            entry = _proxies[index];
            _proxies.RemoveAt(index);
            _interceptors.RemoveAt(index);
        }

        Shutdown(entry);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>关闭全部代理，返回关闭数量。</summary>
    public int CloseAll()
    {
        RunningProxyEntry[] entries;
        lock (_lock)
        {
            entries = _proxies.ToArray();
            _proxies.Clear();
            _interceptors.Clear();
        }

        foreach (var entry in entries)
        {
            Shutdown(entry);
        }

        if (entries.Length > 0)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return entries.Length;
    }

    private void Shutdown(RunningProxyEntry entry)
    {
        try
        {
            entry.Proxy.Shutdown();
        }
        catch (Exception)
        {
            // 关闭失败不阻断列表刷新（拦截器内部已忽略关闭异常）
        }
    }

    private int NextId()
    {
        lock (_lock)
        {
            return _proxies.Select(proxy => proxy.Id).Prepend(0).Max() + 1;
        }
    }
}
