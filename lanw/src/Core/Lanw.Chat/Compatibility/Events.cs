using Lanw.Chat.Connection;

namespace Lanw.Chat.Events;

/// <summary>登录成功事件（由 Codexus.Development.SDK.Event.EventLoginSuccess 移植）。</summary>
public class EventLoginSuccess(IGameConnection connection) {
    public IGameConnection Connection { get; } = connection;
}

/// <summary>连接关闭事件（由 Codexus.Development.SDK.Event.EventConnectionClosed 移植）。</summary>
public class EventConnectionClosed(IGameConnection connection) {
    public IGameConnection Connection { get; } = connection;
}
