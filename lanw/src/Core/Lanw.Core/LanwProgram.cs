using Lanw.Core.Utils;

namespace Lanw.Core;

/// <summary>
/// 程序级常量（由 Nirvana.Common.PublicProgram 移植，自研）。
/// </summary>
public static class LanwProgram {
    // 更新器版本
    public const string UpdateVersion = "1.0.0";

    // lanw 版本
    public const string Version = "0.1.0";
    public const int VersionId = 1;

    // 是最新版本
    public static bool LatestVersion = true;

    // 检查更新的模式 win | linux | mac
    public static readonly string Mode = Tools.DetectOperatingSystemMode();

    // arm64 | x64
    public static readonly string Arch = Tools.DetectArchitectureMode();

    // 是否是发布版本
    public static readonly bool Release = Tools.IsReleaseVersion();
}