using Lanw.DevPlugin.Enums;

namespace Lanw.DevPlugin.Events;

[AttributeUsage(AttributeTargets.Class)]
public class RegisterEvent(EnumProtocolVersion version = EnumProtocolVersion.All) : Attribute {
    public EnumProtocolVersion Version { get; } = version;
}
