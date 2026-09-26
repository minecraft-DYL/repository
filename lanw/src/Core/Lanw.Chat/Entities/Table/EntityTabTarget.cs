using DotNetty.Buffers;
using Lanw.Chat.Extensions;

namespace Lanw.Chat.Entities.Table;

/// <summary>玩家列表（Tab）玩家名/签名实体，由 Nirvana.Chat.Entities.Table.EntityTabTarget 移植。</summary>
public class EntityTabTarget {
    private readonly bool _hasSignature;

    private readonly string _name;
    private readonly string? _signature;
    private readonly string _value;

    public EntityTabTarget(IByteBuffer buffer)
    {
        _name = buffer.ReadStringFromBuffer(short.MaxValue);
        _value = buffer.ReadStringFromBuffer(short.MaxValue);
        _hasSignature = buffer.ReadBoolean();
        if (_hasSignature) {
            _signature = buffer.ReadStringFromBuffer(short.MaxValue);
        }
    }

    public void WriteToBuffer(IByteBuffer buffer)
    {
        buffer.WriteStringToBuffer(_name);
        buffer.WriteStringToBuffer(_value);
        buffer.WriteBoolean(_hasSignature);
        if (_hasSignature) {
            buffer.WriteStringToBuffer(_signature ?? string.Empty);
        }
    }
}
