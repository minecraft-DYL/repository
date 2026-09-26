using DotNetty.Transport.Channels;
using Lanw.DevPlugin.Entities;
using Lanw.DevPlugin.Enums;

namespace Lanw.DevPlugin;

public abstract class BGameConnection {
    public IChannel? ClientChannel;
    public required InterceptorConfig Config;
    public EnumProtocolVersion ProtocolVersion;
    public IChannel? ServerChannel;
    public EnumConnectionState State;
}
