using DotNetty.Buffers;

namespace Lanw.Chat.Entities.Table;

/// <summary>玩家列表（Tab）条目基类，由 Nirvana.Chat.Entities.Table.EntityTabBase 移植。</summary>
public class EntityTabBase {
    private readonly List<long> _uniqueIdList = [];
    public string? NewName = null;
    public string? OldName = null;

    public string Text = string.Empty;

    protected EntityTabBase(IByteBuffer buffer)
    {
        _uniqueIdList.Add(buffer.ReadLong()); // readUniqueId();
        _uniqueIdList.Add(buffer.ReadLong()); // readUniqueId();
    }

    protected void WriteToBuffer(IByteBuffer buffer)
    {
        foreach (var uniqueId in _uniqueIdList) {
            buffer.WriteLong(uniqueId);
        }
    }
}
