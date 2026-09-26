using System.Text.Json.Serialization;

namespace Lanw.Chat.Entities.Nirvana;

/// <summary>文本实体（由 NirvanaAPI.Entities.Nirvana.EntityText 移植）。</summary>
public class EntityText {
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

/// <summary>模式实体（由 NirvanaAPI.Entities.Nirvana.EntityMode 移植），用于分发服务端消息。</summary>
public class EntityMode {
    [JsonPropertyName("mode")]
    public string? Mode { get; set; }
}

/// <summary>消息实体（由 NirvanaAPI.Entities.Nirvana.EntityMessage 移植），用于承载服务端提示文本。</summary>
public class EntityMessage {
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
