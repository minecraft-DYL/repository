using Lanw.Chat.Events;
using Lanw.Chat.Manager;
using Lanw.Chat.Message;

namespace Lanw.Chat.Manager;

/// <summary>
///     聊天室注册与连接事件编排，由 Nirvana.Chat.Manager.ChatManager 移植。
/// </summary>
public static class ChatManager {
    public static void Register()
    {
        foreach (var channel in MessageChannels.AllVersions) {
            EventManager.Instance.RegisterHandler<EventLoginSuccess>(channel, OnLoginSuccess);
        }

        EventManager.Instance.RegisterHandler<EventConnectionClosed>(MessageChannels.ChannelConnection, OnConnectionClosed);
    }

    private static void OnLoginSuccess(EventLoginSuccess args)
    {
        // 登录成功
        _ = ChatMessage.StartAsync(args.Connection);
    }

    private static void OnConnectionClosed(EventConnectionClosed args)
    {
        // 连接关闭
        _ = ChatMessage.RemoveJoin(args.Connection);
    }
}
