using System.Text.Json.Serialization;
using Lanw.Core.Entities;

namespace Lanw.Public.Entities.Nirvana;

/// <summary>
/// 涅槃云登录响应（由 Nirvana.Public.Entities.Nirvana.EntityNirvanaLogin 移植，自研）。
/// </summary>
public class EntityNirvanaLogin : EntityResponseBase
{
    [JsonPropertyName("online")]
    public string? Token { get; set; }
}