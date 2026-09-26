using System.Text.Json.Serialization;
using Lanw.Chat.Connection;

namespace Lanw.Chat.Entities;

/// <summary>加入/退出聊天室实体（IRC 上行），由 Nirvana.Chat.Entities.EntityChatJoin 移植。</summary>
public class EntityChatJoin {
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "join";

    [JsonPropertyName("nickName")]
    public required string NickName { get; set; }

    [JsonPropertyName("gameId")]
    public string GameId { get; set; } = "-1";

    public bool Equals(string gameId)
    {
        return GameId.Equals(gameId);
    }

    public bool Equals(string gameId, string nickName)
    {
        return Equals(gameId) && NickName.Equals(nickName);
    }

    public bool Equals(IGameConnection gameConnection)
    {
        return Equals(gameConnection.GameId, gameConnection.NickName);
    }

    public bool Equals(EntityChatJoin entityChatJoin)
    {
        return Equals(entityChatJoin.GameId, entityChatJoin.NickName);
    }
}
