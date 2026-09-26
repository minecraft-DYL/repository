using Wyolm.Core.Models;

namespace Wyolm.Core.Services;

/// <summary>枚举盘符。</summary>
public static class DriveService
{
    /// <summary>列出所有就绪的本地固定盘 / 可移动盘，并标注哪个是系统盘。</summary>
    public static IReadOnlyList<DriveInfoModel> GetDrives(bool includeNonReady = false)
    {
        var systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? @"C:\";
        var result = new List<DriveInfoModel>();

        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (!d.IsReady && !includeNonReady)
                {
                    result.Add(new DriveInfoModel
                    {
                        Letter = d.Name.TrimEnd('\\'),
                        RootPath = d.Name,
                        DriveType = d.DriveType.ToString(),
                        IsReady = false,
                    });
                    continue;
                }

                result.Add(new DriveInfoModel
                {
                    Letter = d.Name.TrimEnd('\\'),
                    RootPath = d.Name,
                    Label = d.IsReady ? SafeLabel(d) : null,
                    FileSystem = d.IsReady ? SafeFileSystem(d) : null,
                    DriveType = d.DriveType.ToString(),
                    TotalBytes = d.IsReady ? d.TotalSize : 0,
                    FreeBytes = d.IsReady ? d.AvailableFreeSpace : 0,
                    IsReady = d.IsReady,
                    IsSystem = string.Equals(d.Name.TrimEnd('\\'), systemRoot.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase),
                });
            }
            catch (Exception)
            {
                // 某些卷在枚举时会抛异常（例如未插入的读卡器），直接跳过。
            }
        }

        return result
            .OrderByDescending(x => x.IsSystem)
            .ThenBy(x => x.Letter, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? SafeLabel(DriveInfo d)
    {
        try { return d.VolumeLabel; } catch (Exception) { return null; }
    }

    private static string? SafeFileSystem(DriveInfo d)
    {
        try { return d.DriveFormat; } catch (Exception) { return null; }
    }
}
