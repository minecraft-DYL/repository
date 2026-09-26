namespace Lanw.DevPlugin.Events.Event;

public interface IEventCreateInterceptor {
    bool OnCreateInterceptor(int port);
}
