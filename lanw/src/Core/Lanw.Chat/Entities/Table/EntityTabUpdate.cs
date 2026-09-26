using DotNetty.Buffers;
using Lanw.Chat.Extensions;

namespace Lanw.Chat.Entities.Table;

/// <summary>玩家列表（Tab）更新玩家包实体，由 Nirvana.Chat.Entities.Table.EntityTabUpdate 移植。</summary>
public class EntityTabUpdate : EntityTabBase {
    private readonly byte[] _bytes; // 剩余的
    private readonly bool _displayName;

    public EntityTabUpdate(IByteBuffer buffer) : base(buffer)
    {
        _displayName = buffer.ReadBoolean();
        if (_displayName) {
            Text = buffer.ReadStringFromBuffer(short.MaxValue);
        }

        _bytes = buffer.ReadByteArrayReadableBytes();
    }

    public new void WriteToBuffer(IByteBuffer buffer)
    {
        base.WriteToBuffer(buffer);
        buffer.WriteBoolean(_displayName);
        if (_displayName) {
            var newText = Text;
            if (OldName != null && NewName != null) {
                newText = newText.Replace(OldName, NewName);
            }

            buffer.WriteStringToBuffer(newText);
        }

        buffer.WriteBytes(_bytes);
    }
}
