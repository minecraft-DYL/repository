using System.Text.Json.Serialization;
using Lanw.Core.Entities.Login;
using Lanw.Development;

namespace Lanw.Public.Entities.NEL;

/// <summary>
/// 运行中的代理（由 Nirvana.Public.Entities.NEL.RunningProxy 移植，自研，t14）。
/// 说明：原参考构造为 RunningProxy(Interceptor)，强类型 Interceptor 属 Nirvana.Development
/// （由 t20 移植为 Lanw.Development）；在 t20 落地前，此处以“代理运行参数 + 关闭句柄”构造，
/// JSON 输出形状（is_rental / local_address / local_port / nick_name / server_name）与参考一致，
/// t20 落地后可恢复强类型构造重载。
/// </summary>
public class RunningProxy : EntityProxyBase
{
    // 拦截器（由 Nirvana.Public.Entities.NEL.RunningProxy.Interceptor 移植，t20 落地后恢复）
    public readonly Interceptor Interceptor;

    // 关闭服务句柄（对应参考 Interceptor.ShutdownAsync）
    private readonly Action _shutdown;

    /// <summary>
    /// 参考源构造：RunningProxy(Interceptor)（t17 恢复，供 ActiveGameAndProxies/InterceptorManager 使用）。
    /// </summary>
    public RunningProxy(Interceptor interceptor)
    {
        Interceptor = interceptor;
        _shutdown = interceptor.ShutdownAsync;
        IsRental = Interceptor.CurrentConfig.IsRental;
        NickName = Interceptor.CurrentConfig.NickName;
        LocalPort = Interceptor.CurrentConfig.LocalPort;
        ServerName = Interceptor.CurrentConfig.ServerName;
        LocalAddress = Interceptor.CurrentConfig.LocalAddress;
    }

    public RunningProxy(bool isRental, string localAddress, int localPort, string nickName, string serverName, Action? shutdown = null)
    {
        // lanw 兼容构造（非参考源）：无强类型拦截器，故 Interceptor 为空。
        Interceptor = null!;
        _shutdown = shutdown ?? (static () => { });
        IsRental = isRental;
        NickName = nickName;
        LocalPort = localPort;
        ServerName = serverName;
        LocalAddress = localAddress;
    }

    [JsonPropertyName("is_rental")]
    [JsonInclude]
    public bool IsRental { get; init; }

    [JsonPropertyName("local_address")]
    [JsonInclude]
    public string LocalAddress { get; init; }

    [JsonPropertyName("local_port")]
    [JsonInclude]
    public int LocalPort { get; init; }

    [JsonPropertyName("nick_name")]
    [JsonInclude]
    private string NickName { get; init; }

    [JsonPropertyName("server_name")]
    [JsonInclude]
    public string ServerName { get; init; }

    /**
     * 关闭服务
     */
    public void Shutdown()
    {
        _shutdown();
    }

    /**
     * 获取游戏昵称
     */
    public override string GetNickName()
    {
        return NickName;
    }
}
