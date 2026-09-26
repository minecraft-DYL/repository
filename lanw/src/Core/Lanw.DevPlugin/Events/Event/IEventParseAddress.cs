using Lanw.DevPlugin.Entities;

namespace Lanw.DevPlugin.Events.Event;

public interface IEventParseAddress {
    void OnParseAddress(InterceptorConfig config);
}
