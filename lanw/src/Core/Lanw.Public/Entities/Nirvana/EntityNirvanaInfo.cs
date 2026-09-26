using System.Text.Json.Serialization;
using Lanw.Core.Utils;

namespace Lanw.Public.Entities.Nirvana;

/// <summary>
/// 涅槃云账号信息响应（由 Nirvana.Public.Entities.Nirvana.EntityNirvanaInfo 移植，自研）。
/// 说明：<c>days</c> 改为非必填——错误信封（如 <c>{"code":22,...}</c>）不带 days，
/// 原 <c>required</c> 会让「登录态过期」这类正常业务错误在解析阶段就抛异常，掩盖真实错误码。
/// </summary>
public class EntityNirvanaInfo
{
    [JsonPropertyName("code")]
    public int? Code { get; set; }

    [JsonPropertyName("days")]
    public double Days { get; set; }

    [JsonPropertyName("msg")]
    [JsonConverter(typeof(FirstStringConverter))]
    public string? Message { get; set; }
}
