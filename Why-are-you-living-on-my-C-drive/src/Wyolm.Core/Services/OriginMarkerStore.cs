using System.Text.Json;
using Wyolm.Core.Models;

namespace Wyolm.Core.Services;

/// <summary>
/// 来源标记的读写。
/// <para>
/// 标记文件 <c>.wyolm-origin.json</c> 会被写在"被搬走后的真实目录"里，
/// 里面记着它原本住在哪。有了它，"扫描别的盘符然后自动接回原位"才成立。
/// </para>
/// </summary>
public static class OriginMarkerStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>把标记写进目录。</summary>
    public static void Write(string directoryPath, OriginMarker marker)
    {
        var file = Path.Combine(directoryPath, OriginMarker.FileName);
        var json = JsonSerializer.Serialize(marker, JsonOptions);

        // 先写临时文件再替换，避免写到一半断电留下一个坏 JSON。
        var temp = file + ".tmp";
        File.WriteAllText(temp, json, System.Text.Encoding.UTF8);

        if (File.Exists(file))
        {
            File.Replace(temp, file, null, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temp, file);
        }

        try
        {
            File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.Hidden);
        }
        catch (Exception)
        {
            // 设成隐藏失败无所谓，不影响功能。
        }
    }

    /// <summary>读取目录里的标记，没有或损坏时返回 null。</summary>
    public static OriginMarker? Read(string directoryPath)
    {
        try
        {
            var file = Path.Combine(directoryPath, OriginMarker.FileName);
            if (!File.Exists(file)) return null;

            var json = File.ReadAllText(file, System.Text.Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json)) return null;

            return JsonSerializer.Deserialize<OriginMarker>(json, JsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>删除标记（例如用户选择把它重新接回原位之后）。</summary>
    public static void Delete(string directoryPath)
    {
        try
        {
            var file = Path.Combine(directoryPath, OriginMarker.FileName);
            if (File.Exists(file))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
        }
        catch (Exception)
        {
            // 忽略
        }
    }

    /// <summary>构造一个标记。</summary>
    public static OriginMarker Create(string originalPath, string currentPath, LinkKind kind,
        long sizeBytes = 0, long fileCount = 0, string? note = null) => new()
        {
            OriginalPath = PathGuard.Normalize(originalPath),
            CurrentPath = PathGuard.Normalize(currentPath),
            LinkKind = kind,
            SizeBytes = sizeBytes,
            FileCount = fileCount,
            MachineName = Environment.MachineName,
            Note = note,
        };
}
