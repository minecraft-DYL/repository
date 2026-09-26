using System.Text.Json.Serialization;

namespace Lanw.Chat.Entities;

/// <summary>聊天配置实体（IRC 下行），由 Nirvana.Chat.Entities.EntityChatConfig 移植。</summary>
public class EntityChatConfig {
    [JsonPropertyName("heartbeats")]
    public List<string> Heartbeats { get; set; } = [];

    [JsonPropertyName("players")]
    public List<EntityChatPlayer> Players { get; set; } = [];

    // 随机获取一个心跳内容
    public string GetHeartbeat()
    {
        return Heartbeats.Count == 0 ? string.Empty : Heartbeats[Random.Shared.Next(Heartbeats.Count)];
    }
}
