using DotNetty.Common.Utilities;
using Lanw.Development.Connection;

namespace Lanw.Development.Utils;

public static class ChannelAttribute {
    public static readonly AttributeKey<GameConnection> Connection = AttributeKey<GameConnection>.ValueOf("Connection");
}
