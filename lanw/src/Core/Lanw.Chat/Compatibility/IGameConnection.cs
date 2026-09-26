using DotNetty.Transport.Channels;
using Lanw.Chat.Enums;

namespace Lanw.Chat.Connection;

/// <summary>
///     游戏连接契约（对应参考源 Codexus.Development.SDK.Connection.GameConnection 中被聊天室使用的部分）。
///
///     聊天室只依赖连接的：游戏标识 + 玩家昵称 + 协议版本 + 客户端 Channel（用于下发客户端数据包）。
///     以接口形式声明，避免与 Lanw.Development（代理/拦截器任务）中的具体 GameConnection 重复定义；
///     代理层 GameConnection 落地后只需实现本接口即可直接接入聊天室。
/// </summary>
public interface IGameConnection {
    /// <summary>游戏实例标识，默认 "-1"。</summary>
    string GameId { get; }

    /// <summary>玩家昵称。</summary>
    string NickName { get; }

    /// <summary>协议版本。</summary>
    EnumProtocolVersion ProtocolVersion { get; }

    /// <summary>客户端 Channel（用于把聊天消息写回游戏客户端）。</summary>
    IChannel ClientChannel { get; }
}
