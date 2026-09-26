using System.Text.Json.Nodes;

namespace Lanw.Core.Entities;

public interface IConfigValue {
    /// <summary>是否为默认值。</summary>
    bool IsDefault();

    /// <summary>获取值，值为 null 时返回默认值。</summary>
    object? GetValueTo();

    /// <summary>名称是否相同（忽略大小写）。</summary>
    bool EqualsName(string name);

    /// <summary>添加到 Json Object。</summary>
    void ToAdd(JsonObject jsonObj);

    /// <summary>设置值。</summary>
    void SetFrom(object? value);

    /// <summary>设置默认值。</summary>
    void SetDefaultFrom(object? value);
}