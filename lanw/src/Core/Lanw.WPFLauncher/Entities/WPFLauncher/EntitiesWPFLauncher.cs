using System.Text.Json.Serialization;
using Lanw.Core.Entities;

namespace Lanw.WPFLauncher.Entities.WPFLauncher;

// ReSharper disable once InconsistentNaming
/// <summary>
/// X19 多实体响应包装（由 Nirvana.WPFLauncher.Entities.WPFLauncher.EntitiesWPFLauncher 移植，自研）。
/// 供账号自动登录校验（GetFreeSkinListAsync 等）使用。
/// </summary>
public class EntitiesWPFLauncher<T> : EntityWPFResponse
{
    [JsonPropertyName("entities")]
    public T[]? Data { get; init; }

    public new T[] SafeEntity()
    {
        base.SafeEntity();
        return Data ?? throw new EntityX19Exception(Message, this);
    }
}