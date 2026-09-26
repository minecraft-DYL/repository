using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.Converter;

/// <summary>
/// 网易接口中 int/string 可互换字段的转换器（由 Nirvana.WPFLauncher.Entities.Converter.NetEaseIntConverter 移植，自研）。
/// </summary>
public class NetEaseIntConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType != JsonTokenType.Number ? reader.GetString() ?? string.Empty : reader.GetInt32().ToString();
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}