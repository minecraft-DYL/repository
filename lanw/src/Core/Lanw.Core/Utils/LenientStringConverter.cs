using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lanw.Core.Utils;

/// <summary>
/// 宽松字符串转换器：把「本该是字符串、但服务端实际发成 对象/数组/数字/布尔」的字段安全降级为字符串，
/// 而不是抛出 <see cref="JsonException"/> 把原始英文解析错误冒泡到 UI。
/// 规则：String 原样；Null → null；对象/数组 → 取第一个字符串值（如 <c>{"message":"..."}</c>）；
/// 数字/布尔 → 其字面量。找不到字符串时返回 null。
/// 对应涅槃云信封里 <c>data</c> 在成功时为字符串、失败时为 <c>{"message":"..."}</c> 的两种形态。
/// </summary>
public class LenientStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.StartArray:
            case JsonTokenType.StartObject:
                return ReadFirstString(ref reader);
            case JsonTokenType.Number:
            case JsonTokenType.True:
            case JsonTokenType.False:
                using (var document = JsonDocument.ParseValue(ref reader))
                {
                    return document.RootElement.ToString();
                }
            default:
                return null;
        }
    }

    /// <summary>消费整个容器，返回其中遇到的第一个字符串（保持读者位置在容器结束处，避免序列化器状态错乱）。</summary>
    private static string? ReadFirstString(ref Utf8JsonReader reader)
    {
        var depth = 1;
        string? result = null;

        while (depth > 0 && reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartArray:
                case JsonTokenType.StartObject:
                    depth++;
                    break;
                case JsonTokenType.EndArray:
                case JsonTokenType.EndObject:
                    depth--;
                    break;
                case JsonTokenType.String when result == null:
                    result = reader.GetString();
                    break;
            }
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
