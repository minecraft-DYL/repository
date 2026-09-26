using DotNetty.Buffers;
using Lanw.DevPlugin;
using Lanw.DevPlugin.Enums;
using Lanw.DevPlugin.Extensions;
using Lanw.DevPlugin.Packet;
using Serilog;

namespace Lanw.Heypixel.Play;

/// <summary>
/// Play 阶段队伍封包（由 Nirvana.Heypixel.Play.SaClientboundSetPlayerTeamPacket 移植）。
/// </summary>
public class SaClientboundSetPlayerTeamPacket : FPacket {
    public static readonly RegisterPacket RegisterPacket = new(EnumConnectionState.Play, EnumPacketDirection.ClientBound, 96, HeypixelProtocol.GameId, EnumProtocolVersion.V1206);

    private string? _teamName; // 团队名称

    public override void ReadFromBuffer(BGameConnection connection, IByteBuffer buffer)
    {
        base.ReadFromBuffer(buffer);
        _teamName = buffer.ReadStringFromBuffer();
    }

    public override bool HandlePacket(BGameConnection connection)
    {
        if (_teamName == null) {
            return false;
        }

        if (_teamName.StartsWith("collideRule_")) {
            Log.Information("[Heypixel] Team: {0}", _teamName);
            return true;
        }

        return false;
    }
}
