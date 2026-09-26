using System.Text.Json.Serialization;
using Lanw.Core.Entities;
using Lanw.Core.Utils;

namespace Lanw.Public.Entities.Nirvana;

/// <summary>
/// 涅槃云验证码识别响应（/api/fantnel/captcha）。
/// 与通用 <see cref="EntityResponse{T}"/> 的区别：<c>data</c> 在成功时是识别文本字符串，
/// 失败时可能是对象（如 <c>{"message":"..."}</c>），因此用 <see cref="LenientStringConverter"/>
/// 兜住形状差异，避免序列化直接抛 <c>$.data</c> 转换异常。
/// </summary>
public class EntityCaptchaResponse : EntityResponseBase
{
    [JsonPropertyName("data")]
    [JsonConverter(typeof(LenientStringConverter))]
    public string? Data { get; set; }
}
