namespace Lanw.DevPlugin.Events.Event;

public interface IEventEncryptionRequest {
    bool OnEncryptionRequest(string serverId);
}
