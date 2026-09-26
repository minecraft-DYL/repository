using Lanw.Game.Launcher.Protocol;

namespace Lanw.App.Services;

/// <summary>服务器 MOD 清单中的一项（对应 EntityComponentDownloadInfoResponseSub：资源包名/版本/大小/下载地址）。</summary>
public sealed record ServerModEntry(string Name, int Version, long Size, string Url, string ResMd5)
{
    /// <summary>列表主文案：名称 + 版本。</summary>
    public string DisplayText => Version > 0 ? $"{Name} · v{Version}" : Name;

    /// <summary>体积文案（列表右侧）。</summary>
    public string SizeText => Size <= 0
        ? "大小未知"
        : Size >= 1024 * 1024
            ? $"{Size / 1024 / 1024.0:F1} MB"
            : $"{Size / 1024.0:F0} KB";
}

/// <summary>服务器 MOD / Java 版本元数据（只读，不触发下载）。</summary>
public sealed record ServerModInfo(string JavaVersionText, string McVersionName, IReadOnlyList<ServerModEntry> Mods);

/// <summary>
/// 服务器 MOD 元数据（对应网易「游戏模组组件下载信息」接口 GetNetGameComponentDownloadListAsync）。
/// 一次调用即给出该服需要的 Java 版本、MC 版本名与 MOD 资源包清单——也就是「说明服务器的 JAVA 游戏版本和 mod」。
/// 桌面版无本地 HTTP：由详情页在进程内直调，不产生下载副作用。
/// </summary>
public static class ServerModService
{
    /// <summary>读取指定服务器的 MOD / Java 版本信息。</summary>
    public static async Task<ServerModInfo> GetAsync(string serverId)
    {
        var response = await NPFLauncher.GetNetGameComponentDownloadListAsync(serverId).ConfigureAwait(false);
        var subs = response.SubEntities ?? [];

        var mods = subs
            .Select(sub => new ServerModEntry(
                string.IsNullOrWhiteSpace(sub.ResName) ? "未命名 MOD 包" : sub.ResName,
                sub.ResVersion,
                sub.ResSize,
                sub.ResUrl,
                sub.ResMd5))
            .ToList();

        var first = subs.FirstOrDefault();
        return new ServerModInfo(
            DescribeJava(first?.JavaVersion ?? 0),
            first?.McVersionName ?? string.Empty,
            mods);
    }

    /// <summary>Java 版本文案（网易下发 8 / 17 / 21 …）。</summary>
    public static string DescribeJava(int javaVersion)
        => javaVersion <= 0 ? "未下发" : $"Java {javaVersion}";
}
