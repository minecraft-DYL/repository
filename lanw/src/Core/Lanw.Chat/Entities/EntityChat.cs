using System.Text.Json.Serialization;

namespace Lanw.Chat.Entities;

/// <summary>聊天消息实体（IRC 上行），由 Nirvana.Chat.Entities.EntityChat 移植。</summary>
public class EntityChat {
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "chat";

    [JsonPropertyName("message")]
    public required string Message { get; set; }
}
