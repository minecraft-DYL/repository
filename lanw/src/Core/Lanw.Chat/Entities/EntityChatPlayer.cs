using System.Text.Json.Serialization;

namespace Lanw.Chat.Entities;

/// <summary>聊天室玩家实体（账号 + 玩家列表），由 Nirvana.Chat.Entities.EntityChatPlayer 移植。</summary>
public class EntityChatPlayer {
    [JsonPropertyName("account")]
    public string Account { get; set; } = string.Empty; // 账户

    [JsonPropertyName("players")]
    public EntityChatJoin[] Players { get; set; } = []; // 玩家列表
}
