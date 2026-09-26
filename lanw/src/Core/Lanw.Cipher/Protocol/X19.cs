using Lanw.Core.Utils.CodeTools;
using Lanw.Cipher.Protocol;

namespace Lanw.Cipher.Protocol;

/// <summary>
/// 网易 X19 启动器常量/运行时报（由 Nirvana.WPFLauncher.Protocol.X19 移植的最小面，自研）。
///
/// 说明：完整 X19 属 WPFLauncher 协议层（其调用 X19Extensions 拉取最新版本号）。
/// 此处仅移植 Cipher 依赖的最小成员（Channel / CrcSalt / GetCrcSalt / GameVersion），
/// 供 YggdrasilGenerator / NetEaseConnection 使用；待 WPFLauncher 移植时统一收敛。
/// </summary>
public static class X19
{
    public const string Channel = "netease";

    /// <summary>CRC 盐值（由登录配置注入）。</summary>
    public static string? CrcSalt;

    /// <summary>最新盒子版本号（延迟拉取）。</summary>
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
        var content = NeteaseApi.UpdateNetease.Api<string>("/pl/x19_java_patchlist");
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