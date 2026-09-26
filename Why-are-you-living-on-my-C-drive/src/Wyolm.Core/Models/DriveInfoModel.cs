namespace Wyolm.Core.Models;

/// <summary>一个可用的盘符 / 卷。</summary>
public sealed class DriveInfoModel
{
    public required string Letter { get; init; }          // 例如 "C:"
    public required string RootPath { get; init; }        // 例如 "C:\"
    public string? Label { get; init; }
    public string? FileSystem { get; init; }
    public string? DriveType { get; init; }
    public long TotalBytes { get; init; }
    public long FreeBytes { get; init; }
    public bool IsReady { get; init; }

    /// <summary>是系统盘（Windows 所在盘）。</summary>
    public bool IsSystem { get; init; }

    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);

    public double UsedPercent => TotalBytes <= 0 ? 0 : (double)UsedBytes / TotalBytes * 100.0;

    public string Display => IsReady
        ? $"{Letter}  {Label}  可用 {FormatBytes(FreeBytes)} / {FormatBytes(TotalBytes)}"
        : $"{Letter}  (未就绪)";

    public override string ToString() => Display;

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0 ? $"{value:0} {units[unit]}" : $"{value:0.##} {units[unit]}";
    }
}
