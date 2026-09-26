using System.Text.Json.Serialization;
using Lanw.Core.Entities;

namespace Lanw.WPFLauncher.Entities.WPFLauncher;

// ReSharper disable once InconsistentNaming
/// <summary>
/// X19 单实体响应包装（由 Nirvana.WPFLauncher.Entities.WPFLauncher.EntityWPFLauncher 移植，自研）。
/// </summary>
public class EntityWPFLauncher<T> : EntityWPFResponse
{
    [JsonPropertyName("entity")]
    public T? Data { get; init; }

    public new T SafeEntity()
    {
        base.SafeEntity();
        return Data ?? throw new EntityX19Exception(Message, this);
    }
}