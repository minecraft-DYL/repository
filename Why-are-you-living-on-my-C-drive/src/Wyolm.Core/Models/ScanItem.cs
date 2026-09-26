namespace Wyolm.Core.Models;

/// <summary>扫描出来的一个候选目录。</summary>
public sealed class ScanItem
{
    /// <summary>完整路径。</summary>
    public required string FullPath { get; init; }

    /// <summary>目录名。</summary>
    public required string Name { get; init; }

    /// <summary>它所在的扫描根。</summary>
    public required string RootPath { get; init; }

    /// <summary>真实占用的字节数（不跟随链接）。</summary>
    public long SizeBytes { get; set; }

    public long FileCount { get; set; }
    public long DirectoryCount { get; set; }

    /// <summary>枚举过程中因权限等原因跳过的条目数。</summary>
    public long InaccessibleCount { get; set; }

    /// <summary>若该目录本身就是一个链接，这里是链接类型。</summary>
    public LinkKind LinkKind { get; init; } = LinkKind.None;

    /// <summary>若是链接，指向的目标。</summary>
    public string? LinkTarget { get; init; }

    /// <summary>该目录是否是本工具搬走的（目录里有来源标记）。</summary>
    public bool HasOriginMarker { get; set; }

    /// <summary>来源标记记录的原始路径。</summary>
    public string? OriginalPath { get; set; }

    /// <summary>原始路径当前是否已是链接（说明已经接好了）。</summary>
    public bool OriginAlreadyLinked { get; set; }

    /// <summary>原始路径当前是否存在实体目录（会阻止重新连接）。</summary>
    public bool OriginOccupied { get; set; }

    /// <summary>是不是一个已知的"体积大户"（缓存 / 包管理器 / 容器等），值得优先搬家。</summary>
    public bool IsKnownHeavyPath { get; set; }

    public string? Suggestion { get; set; }

    public bool IsLink => LinkKind != LinkKind.None;

    public string SizeDisplay => DriveInfoModel.FormatBytes(SizeBytes);

    public string KindDisplay => LinkKind switch
    {
        LinkKind.Junction => "目录联接",
        LinkKind.SymbolicLink => "符号链接",
        _ => "真实目录",
    };
}
