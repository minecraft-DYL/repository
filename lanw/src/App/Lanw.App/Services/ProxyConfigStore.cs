using Lanw.Core.Utils;

namespace Lanw.App.Services;

/// <summary>
/// 代理页配置：对应用户可调的拦截器参数（Lanw.DevPlugin.Entities.InterceptorConfig 的用户可编辑子集）。
/// 说明：原 Vue 版 ProxyManager.vue 只能查看/关闭代理，参数由服务器详情页传入；
/// 本页补充「拦截器配置」卡片，因此需要一份可持久化的本机默认配置。
/// </summary>
public sealed class ProxySettings
{
    /// <summary>脱盒拦截开关：关闭后不允许启动新的拦截器（对应 InterceptorConfig 是否生效）。</summary>
    public bool InterceptEnabled { get; set; } = true;

    /// <summary>代理监听地址（InterceptorConfig.LocalAddress）。</summary>
    public string LocalAddress { get; set; } = "127.0.0.1";

    /// <summary>代理监听端口下限（InterceptorConfig.LocalPort；端口被占用时自动向后取可用端口）。</summary>
    public int LocalPort { get; set; } = 25565;

    /// <summary>转发目标地址（InterceptorConfig.ForwardAddress，即白端/盒端真实服务器）。</summary>
    public string ForwardAddress { get; set; } = "127.0.0.1";

    /// <summary>转发目标端口（InterceptorConfig.ForwardPort）。</summary>
    public int ForwardPort { get; set; } = 25565;

    /// <summary>替换后的游戏昵称（InterceptorConfig.NickName，登录包会被改写为该昵称）。</summary>
    public string NickName { get; set; } = string.Empty;

    /// <summary>服务器名称（InterceptorConfig.ServerName，用于 UDP 广播 MOTD）。</summary>
    public string ServerName { get; set; } = "Lanw 代理";

    /// <summary>服务器版本（InterceptorConfig.ServerVersion，参与 MOTD 版本判定）。</summary>
    public string ServerVersion { get; set; } = "1.20";

    /// <summary>模组信息（InterceptorConfig.ModInfo）。</summary>
    public string ModInfo { get; set; } = string.Empty;

    /// <summary>游戏/服务器 ID（InterceptorConfig.GameId，用于包注册表按游戏过滤）。</summary>
    public string GameId { get; set; } = string.Empty;

    /// <summary>是否为租凭服代理（InterceptorConfig.IsRental）。</summary>
    public bool IsRental { get; set; }

    /// <summary>浅拷贝，避免调用方持有的实例被直接改写。</summary>
    public ProxySettings Clone()
    {
        return (ProxySettings)MemberwiseClone();
    }
}

/// <summary>
/// 代理配置持久化：落在 resources/config.json（Lanw.Core.Utils.ConfigUtil，字符串键值）。
/// 与设置页的 nirvanaAccount.json 分开存放，避免与账号配置的键集合互相干扰。
/// </summary>
public static class ProxyConfigStore
{
    private const string KeyInterceptEnabled = "proxyInterceptEnabled";
    private const string KeyLocalAddress = "proxyLocalAddress";
    private const string KeyLocalPort = "proxyLocalPort";
    private const string KeyForwardAddress = "proxyForwardAddress";
    private const string KeyForwardPort = "proxyForwardPort";
    private const string KeyNickName = "proxyNickName";
    private const string KeyServerName = "proxyServerName";
    private const string KeyServerVersion = "proxyServerVersion";
    private const string KeyModInfo = "proxyModInfo";
    private const string KeyGameId = "proxyGameId";
    private const string KeyIsRental = "proxyIsRental";

    /// <summary>读取配置（缺失或非法时回落到默认值）。</summary>
    public static ProxySettings Load()
    {
        var defaults = new ProxySettings();
        return new ProxySettings
        {
            InterceptEnabled = ParseBool(Get(KeyInterceptEnabled, defaults.InterceptEnabled.ToString()), defaults.InterceptEnabled),
            LocalAddress = Get(KeyLocalAddress, defaults.LocalAddress),
            LocalPort = ParsePort(Get(KeyLocalPort, defaults.LocalPort.ToString()), defaults.LocalPort),
            ForwardAddress = Get(KeyForwardAddress, defaults.ForwardAddress),
            ForwardPort = ParsePort(Get(KeyForwardPort, defaults.ForwardPort.ToString()), defaults.ForwardPort),
            NickName = Get(KeyNickName, defaults.NickName),
            ServerName = Get(KeyServerName, defaults.ServerName),
            ServerVersion = Get(KeyServerVersion, defaults.ServerVersion),
            ModInfo = Get(KeyModInfo, defaults.ModInfo),
            GameId = Get(KeyGameId, defaults.GameId),
            IsRental = ParseBool(Get(KeyIsRental, defaults.IsRental.ToString()), defaults.IsRental),
        };
    }

    /// <summary>保存配置到 resources/config.json（目录不存在时自动创建）。</summary>
    public static void Save(ProxySettings settings)
    {
        Directory.CreateDirectory(PathUtil.ResourcePath);
        ConfigUtil.SaveConfig(KeyInterceptEnabled, settings.InterceptEnabled.ToString());
        ConfigUtil.SaveConfig(KeyLocalAddress, settings.LocalAddress);
        ConfigUtil.SaveConfig(KeyLocalPort, settings.LocalPort.ToString());
        ConfigUtil.SaveConfig(KeyForwardAddress, settings.ForwardAddress);
        ConfigUtil.SaveConfig(KeyForwardPort, settings.ForwardPort.ToString());
        ConfigUtil.SaveConfig(KeyNickName, settings.NickName);
        ConfigUtil.SaveConfig(KeyServerName, settings.ServerName);
        ConfigUtil.SaveConfig(KeyServerVersion, settings.ServerVersion);
        ConfigUtil.SaveConfig(KeyModInfo, settings.ModInfo);
        ConfigUtil.SaveConfig(KeyGameId, settings.GameId);
        ConfigUtil.SaveConfig(KeyIsRental, settings.IsRental.ToString());
    }

    /// <summary>配置文件绝对路径（供状态提示/排障使用）。</summary>
    public static string ConfigFilePath => Path.Combine(PathUtil.ResourcePath, "config.json");

    private static string Get(string name, string fallback)
    {
        try
        {
            var value = ConfigUtil.GetConfig(name, fallback);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
        catch (Exception)
        {
            // 配置文件损坏时退回默认值，避免代理页整体不可用
            return fallback;
        }
    }

    private static int ParsePort(string value, int fallback)
    {
        return int.TryParse(value, out var port) && port is > 0 and <= 65535 ? port : fallback;
    }

    private static bool ParseBool(string value, bool fallback)
    {
        return bool.TryParse(value, out var result) ? result : fallback;
    }
}
