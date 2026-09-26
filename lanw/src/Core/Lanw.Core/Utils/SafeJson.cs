using System.Text.Json;

namespace Lanw.Core.Utils;

/// <summary>
/// 安全 JSON 反序列化：把底层的 <see cref="JsonException"/>（英文、带 <c>$</c> 路径、
/// 如 <c>'0xE8' is an invalid start of a value</c>）统一包装成带「哪个接口/什么内容」的中文可读异常，
/// 便于 UI 直接展示，也便于排查是哪个响应形状不符合预期。
/// 用于那些直接对远端原始文本调用 <c>JsonSerializer.Deserialize</c> 的调用点。
/// </summary>
public static class SafeJson
{
    public static T? Deserialize<T>(string? json, string what)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new JsonException($"{what} 响应为空，无法解析。");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException e)
        {
            throw new JsonException($"{what} 响应不是合法 JSON（{Preview(json)}）", e);
        }
    }

    /// <summary>截取一段可读预览（压缩空白、限制长度），用于异常文案。</summary>
    public static string Preview(string? text, int max = 120)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "<空>";
        }

        var collapsed = string.Join(' ', text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length <= max ? collapsed : collapsed[..max] + "…";
    }
}
