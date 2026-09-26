using System.Text.Json.Serialization;
using Lanw.Chat.Entities.Nirvana;

namespace Lanw.Chat.Entities.Packet;

/// <summary>聊天 JSON 文本实体（extra 富文本），由 Nirvana.Chat.Entities.Packet.EntityChatMessage 移植。</summary>
public class EntityChatMessage : EntityText {
    [JsonPropertyName("extra")]
    public required List<EntityChatPart> Extra { get; set; }
}
