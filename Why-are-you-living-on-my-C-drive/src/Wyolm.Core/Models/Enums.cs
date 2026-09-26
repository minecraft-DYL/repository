namespace Wyolm.Core.Models;

/// <summary>目录链接的类型。</summary>
public enum LinkKind
{
    /// <summary>不是链接，是一个真实目录。</summary>
    None = 0,

    /// <summary>
    /// 目录联接（Junction）。创建不需要管理员权限，也不需要开发者模式，
    /// 是本工具默认且最稳妥的方案。
    /// </summary>
    Junction = 1,

    /// <summary>
    /// 目录符号链接（Symbolic Link）。语义更"正统"，可跨卷、可相对路径，
    /// 但创建需要管理员权限或系统已开启开发者模式。
    /// </summary>
    SymbolicLink = 2,
}

/// <summary>本工具对目标文件夹执行的操作种类。</summary>
public enum MigrationMode
{
    /// <summary>只创建链接，不搬运任何数据。用于"东西已经在别的盘了，帮我接回原位"。</summary>
    LinkOnly = 0,

    /// <summary>复制到目标位置，但保留原文件夹（原位置变成一份真实副本，不建链接）。</summary>
    CopyOnly = 1,

    /// <summary>
    /// 移动到目标位置，并在原路径留下链接。这是"把 C 盘的大文件夹搬到 D 盘"的标准操作。
    /// </summary>
    MoveAndLink = 2,
}

/// <summary>迁移完成后的校验强度。</summary>
public enum VerifyLevel
{
    /// <summary>只比对文件数量与目录结构。快，但理论上可能漏掉被静默截断的文件。</summary>
    Structure = 0,

    /// <summary>结构 + 每个文件的大小 + 最后写入时间。默认档位，性价比最高。</summary>
    SizeAndTimestamp = 1,

    /// <summary>结构 + 大小 + 时间 + 全量 SHA-256 内容哈希。最慢，但可以证明字节级一致。</summary>
    ContentHash = 2,
}

/// <summary>一次迁移所处的阶段。崩溃恢复时会依据它判断该回滚还是该收尾。</summary>
public enum MigrationPhase
{
    /// <summary>尚未开始。</summary>
    NotStarted = 0,

    /// <summary>前置检查（路径合法性、剩余空间、占用检测）进行中。</summary>
    Preflight = 1,

    /// <summary>正在把数据复制到目标位置。</summary>
    Copying = 2,

    /// <summary>正在校验目标副本。</summary>
    Verifying = 3,

    /// <summary>正在把原目录改名为备份目录（为建链接腾位置）。</summary>
    RenamingSource = 4,

    /// <summary>正在创建链接。</summary>
    Linking = 5,

    /// <summary>链接已建立，正在清理备份目录（数据在这一步真正离开原盘）。</summary>
    Committing = 6,

    /// <summary>全部完成。</summary>
    Completed = 7,

    /// <summary>失败，且已成功回滚到初始状态。</summary>
    RolledBack = 8,

    /// <summary>失败，回滚也失败了 —— 需要人工介入。</summary>
    RollbackFailed = 9,
}

/// <summary>一次迁移任务的最终结果。</summary>
public enum MigrationOutcome
{
    /// <summary>成功。</summary>
    Success = 0,

    /// <summary>取消（用户主动或应用退出）。原状态未被破坏。</summary>
    Cancelled = 1,

    /// <summary>前置检查未通过，未做任何改动。</summary>
    Rejected = 2,

    /// <summary>中途失败，已自动回滚，原文件夹完好无损。</summary>
    FailedRolledBack = 3,

    /// <summary>中途失败，且回滚未能完成。需要人工处理，详情见日志。</summary>
    FailedRollbackIncomplete = 4,

    /// <summary>试运行：所有检查都通过了，但按要求没有真的动手。</summary>
    DryRunPassed = 5,
}

/// <summary>回滚是否可以安全地执行 / 是否已完成。</summary>
public enum RollbackState
{
    /// <summary>没有需要回滚的东西。</summary>
    NotNeeded = 0,

    /// <summary>正在回滚。</summary>
    InProgress = 1,

    /// <summary>回滚成功，原文件夹已恢复。</summary>
    Succeeded = 2,

    /// <summary>回滚失败，留有半成品，需要人工处理。</summary>
    Failed = 3,
}

/// <summary>迁移任务的执行情形，决定界面上展示哪些列。</summary>
public enum MigrationJobKind
{
    /// <summary>把 C 盘的文件夹搬到别的盘并接回链接（标准"搬家"）。</summary>
    Relocate = 0,

    /// <summary>把已经搬到别处的文件夹重新接回原位置（只建链接）。</summary>
    Reconnect = 1,

    /// <summary>用户自选的任意文件夹之间的迁移。</summary>
    Custom = 2,

    /// <summary>崩溃恢复后自动执行的收尾 / 回滚。</summary>
    Recovery = 3,
}
