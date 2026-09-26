using System.Text.Json.Serialization;

namespace Wyolm.Core.Models;

/// <summary>
/// 写在被搬走的目录里的"来源标记"。
/// <para>
/// 这是"扫描自定义盘符然后接回原位置"能自动工作的关键：搬到 D 盘的文件夹里会留下
/// <c>.wyolm-origin.json</c>，记录它原本住在 C 盘的哪个位置。之后在任意机器上扫描 D 盘，
/// 就能读出这些标记，把链接一个个接回原位。
/// </para>
/// </summary>
public sealed class OriginMarker
{
    /// <summary>标记文件的固定文件名。</summary>
    public const string FileName = ".wyolm-origin.json";

    /// <summary>标记格式版本，方便以后演进。</summary>
    public int Version { get; set; } = 1;

    /// <summary>它原本所在的完整路径（链接应该出现的位置）。</summary>
    public required string OriginalPath { get; set; }

    /// <summary>它现在所在的完整路径。</summary>
    public required string CurrentPath { get; set; }

    /// <summary>标记写入时间（UTC）。</summary>
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>写入标记的机器名。</summary>
    public string? MachineName { get; set; }

    /// <summary>创建链接时使用的类型。</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LinkKind LinkKind { get; set; } = LinkKind.Junction;

    /// <summary>搬运时的数据量，仅用于展示。</summary>
    public long SizeBytes { get; set; }

    public long FileCount { get; set; }

    /// <summary>自由备注。</summary>
    public string? Note { get; set; }
}

/// <summary>历史记录里的一条迁移条目。</summary>
public sealed class MigrationRecord
{
    public required string Id { get; set; }
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MigrationJobKind Kind { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MigrationMode Mode { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MigrationOutcome Outcome { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LinkKind LinkKind { get; set; }

    public required string SourcePath { get; set; }
    public required string DestinationPath { get; set; }
    public string? ActualDestinationPath { get; set; }

    public long BytesMoved { get; set; }
    public long FilesMoved { get; set; }
    public double ElapsedSeconds { get; set; }

    public string? FailureMessage { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RollbackState Rollback { get; set; }

    public string? LogPath { get; set; }

    /// <summary>允许用户给这次操作起个名字，方便以后在回滚中心里认出来。</summary>
    public string? Label { get; set; }
}

/// <summary>
/// 崩溃恢复用的事务日志条目。每一步都会先写日志再执行，
/// 所以进程被强杀之后，重新打开应用仍然知道"当时做到哪一步了"。
/// </summary>
public sealed class JournalEntry
{
    public required string JobId { get; set; }
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MigrationPhase Phase { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MigrationMode Mode { get; set; }

    public required string SourcePath { get; set; }
    public required string DestinationPath { get; set; }

    /// <summary>源目录被改名后临时占用的备份路径。</summary>
    public string? BackupPath { get; set; }

    /// <summary>已经确认复制完成的目标路径（回滚时要删掉它）。</summary>
    public string? CopiedPath { get; set; }

    /// <summary>
    /// 目标目录在这次作业之前就已经存在（即用了合并模式）。
    /// <para>恢复逻辑靠它判断"能不能整个删掉目标目录"：如果是本次新建的，整棵删掉是安全的；
    /// 如果之前就有内容，就绝不敢动，只能交给用户人工处理。</para>
    /// </summary>
    public bool DestinationExistedBefore { get; set; }

    /// <summary>链接是否已经建好。</summary>
    public bool LinkCreated { get; set; }

    public string? Message { get; set; }

    /// <summary>该 job 是否已经终结（成功或已回滚），终结后恢复逻辑会跳过它。</summary>
    public bool Finished { get; set; }
}
