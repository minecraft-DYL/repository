using System.Text.Json.Serialization;
using Lanw.Chat.Entities.Nirvana;

namespace Lanw.Chat.Entities.Packet;

/// <summary>聊天 JSON 文本片段（颜色 + 文本），由 Nirvana.Chat.Entities.Packet.EntityChatPart 移植。</summary>
public class EntityChatPart : EntityText {
    [JsonPropertyName("color")]
    public required string Color { get; set; }
}
