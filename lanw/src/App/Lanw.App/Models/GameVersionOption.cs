namespace Lanw.App.Models;

/// <summary>
/// 启动管理页的版本选项（对应原 Vue/后端 McDownloadVersion 的 mc_version 口径）：
/// <see cref="VersionId"/> 即 EnumGameVersion 的枚举数值，直接作为 EntityLaunchGame.GameVersionId 使用
/// （Lanw.Game.Launcher 内部通过 GameVersionConverter.Convert 还原枚举）。
/// </summary>
public sealed record GameVersionOption(int VersionId, string DisplayName)
{
    /// <summary>展示文本。</summary>
    public override string ToString() => DisplayName;
}
