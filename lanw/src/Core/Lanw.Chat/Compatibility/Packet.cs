using DotNetty.Buffers;
using Lanw.Chat.Connection;
using Lanw.Chat.Enums;

namespace Lanw.Chat.Packet.IPacket;

/// <summary>
///     数据包契约（由 NirvanaAPI / Nirvana.DevPlugin 的 IPacket 中被聊天室使用的部分移植）。
///
///     true: 不向白端发送包；false: 向白端发送包。
/// </summary>
public interface IPacket {
    int PacketId { get; set; }

    EnumProtocolVersion ProtocolVersion { get; set; }

    EnumConnectionState State { get; }

    EnumPacketDirection Direction { get; }

    void ReadFromBuffer(IGameConnection connection, IByteBuffer buffer);

    void ReadFromBuffer(IByteBuffer buffer);

    void WriteToBuffer(IByteBuffer buffer);

    bool HandlePacket(IGameConnection connection)
    {
        return false;
    }
}

/// <summary>
///     抽象数据包基类（对应参考源 NirvanaAPI 的 APacket：仅保留聊天室用到的注册元信息与读写钩子）。
/// </summary>
public abstract class APacket : IPacket {
    /// <summary>注册元信息（由构造参数决定）。</summary>
    public readonly bool Skip;

    protected APacket(EnumConnectionState state, EnumPacketDirection direction, int packetId, EnumProtocolVersion version, bool skip = false)
    {
        State = state;
        Direction = direction;
        PacketId = packetId;
        ProtocolVersion = version;
        Skip = skip;
    }

    public EnumConnectionState State { get; }

    public EnumPacketDirection Direction { get; }

    public int PacketId { get; set; }

    public EnumProtocolVersion ProtocolVersion { get; set; }

    public void ReadFromBuffer(IGameConnection connection, IByteBuffer buffer)
    {
        ReadFromBuffer(buffer);
    }

    public abstract void ReadFromBuffer(IByteBuffer buffer);

    public abstract void WriteToBuffer(IByteBuffer buffer);

    public abstract bool HandlePacket(IGameConnection connection);
}
