using Lanw.Core.Utils.CodeTools;
using Lanw.WPFLauncher.Http;

namespace Lanw.WPFLauncher.Protocol;

/// <summary>
/// 网易 X19 启动器常量/运行时报（由 Nirvana.WPFLauncher.Protocol.X19 移植，自研）。
/// </summary>
public static class X19
{
    public const string Channel = "netease";

    /// <summary>CRC 盐值。</summary>
    public static string? CrcSalt;

    /// <summary>最新盒子版本号。</summary>
    public static string GameVersion => GetLatestVersion();

    public static string GetCrcSalt()
    {
        return CrcSalt ?? throw new ErrorCodeException(ErrorCode.CrcSaltNotSet);
    }

    /// <summary>
    /// @return 最新盒子版本号
    /// </summary>
    private static string GetLatestVersion()
    {
        var content = X19Extensions.UpdateNetease.Api<string>("/pl/x19_java_patchlist");
        ArgumentException.ThrowIfNullOrEmpty(content);

        const string size = "\":{\"size\":";

        var pos = content.LastIndexOf(size, StringComparison.Ordinal);
        if (pos == -1)
        {
            throw new ErrorCodeException(ErrorCode.NotVersionByLauncher);
        }

        content = content[..pos];
        pos = content.LastIndexOf('\"');

        return pos >= 0 ? content[(pos + 1)..] : throw new ErrorCodeException(ErrorCode.NotVersionByLauncher);
    }
}