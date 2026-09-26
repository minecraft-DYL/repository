namespace Lanw.Public.Entities.NEL;

/// <summary>
/// 玩家改名实体（由 Nirvana.Public.Entities.NEL.EntityNewName 移植）。
/// </summary>
public class EntityNewName(string? id, string? name) {
    public string? Id { get; } = id;
    public string? Name { get; } = name;
}
