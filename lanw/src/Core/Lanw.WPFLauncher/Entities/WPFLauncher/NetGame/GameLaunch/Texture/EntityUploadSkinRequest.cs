using System.Text.Json.Serialization;
using Lanw.WPFLauncher.Entities.WPFLauncher.Minecraft;

namespace Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameLaunch.Texture;

/// <summary>
/// 上传本地皮肤请求（lanw 自研设计：参考源 Fantnel/Nirvana 无“上传本地皮肤”端点，
/// 为皮肤页“上传本地皮肤”功能扩展。File 为皮肤 PNG 字节的 Base64 文本，
/// 其余字段与用户游戏贴图语义保持一致）。
/// 对应端点 /user-game-skin。
/// </summary>
public class EntityUploadSkinRequest
{
    [JsonPropertyName("file")]
    public required string File { get; set; }

    [JsonPropertyName("file_name")]
    public required string FileName { get; set; }

    [JsonPropertyName("client_type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EnumGameClientType ClientType { get; set; }

    [JsonPropertyName("skin_type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EnumTextureType SkinType { get; set; }

    [JsonPropertyName("skin_mode")]
    public int SkinMode { get; set; }
}
