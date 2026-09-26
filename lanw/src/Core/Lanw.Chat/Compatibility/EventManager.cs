namespace Lanw.Chat.Manager;

/// <summary>
///     事件管理器（由 Codexus.Development.SDK.Manager.EventManager 中被聊天室使用的部分移植）。
///
///     参考实现按事件类型 + 通道注册处理器；此处保留相同的注册/派发语义，
///     便于代理层（Lanw.Development）落地后直接调用 Trigger 派发真实连接事件。
/// </summary>
public class EventManager {
    private static readonly Lazy<EventManager> LazyInstance = new(() => new EventManager());
    private readonly List<(string Channel, Type EventType, Delegate Handler)> _handlers = [];

    public static EventManager Instance => LazyInstance.Value;

    /// <summary>注册处理器。</summary>
    public void RegisterHandler<TEvent>(string channel, Action<TEvent> handler)
    {
        _handlers.Add((channel, typeof(TEvent), handler));
    }

    /// <summary>注销某事件类型的全部处理器。</summary>
    public void UnregisterHandler<TEvent>()
    {
        _handlers.RemoveAll(item => item.EventType == typeof(TEvent));
    }

    /// <summary>按通道派发事件（同步回调，异常与参考实现一致向上抛出由调用方处理）。</summary>
    public void Trigger<TEvent>(string channel, TEvent args)
    {
        foreach (var (handlerChannel, eventType, handler) in _handlers.ToArray()) {
            if (handlerChannel == channel && eventType == typeof(TEvent)) {
                ((Action<TEvent>)handler).Invoke(args);
            }
        }
    }

    /// <summary>清空全部处理器（测试与关闭流程使用）。</summary>
    public void Clear()
    {
        _handlers.Clear();
    }
}
