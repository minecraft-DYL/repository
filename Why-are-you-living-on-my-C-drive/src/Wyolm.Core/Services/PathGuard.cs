using System.Runtime.InteropServices;
using Wyolm.Core.Interop;
using Wyolm.Core.Models;

namespace Wyolm.Core.Services;

/// <summary>
/// 路径安全守卫。
/// <para>
/// 这是整个工具里最不该出错的一块。搬家 + 建链接做错一步就可能让系统起不来，
/// 所以这里对"源目录能不能动"采取了非常保守的白名单式否决：宁可挡住一个合法需求，
/// 也不放行一个危险路径。
/// </para>
/// </summary>
public static class PathGuard
{
    /// <summary>这些路径绝对不允许作为被搬走的源目录。</summary>
    private static readonly string[] HardBlockedPaths =
    [
        @"C:\",
        @"C:\Windows",
        @"C:\Program Files",
        @"C:\Program Files (x86)",
        @"C:\ProgramData",
        @"C:\Users",
        @"C:\Boot",
        @"C:\Recovery",
        @"C:\System Volume Information",
        @"C:\$Recycle.Bin",
        @"C:\$WinREAgent",
        @"C:\PerfLogs",
        @"C:\Intel",
        @"C:\MSOCache",
        @"C:\Config.Msi",
        @"C:\Documents and Settings",
    ];

    /// <summary>AppData 这一类目录是"绝对不能再往下切一刀"的，切了应用会大面积崩。</summary>
    private static readonly string[] DangerousAncestorSuffixes =
    [
        @"\AppData",
        @"\AppData\Local",
        @"\AppData\LocalLow",
        @"\AppData\Roaming",
        @"\AppData\Local\Temp",
        @"\Local Settings",
        @"\Application Data",
    ];

    /// <summary>这些属于"可以做但很危险"，只给警告不拦截 —— 很多人确实想搬 Downloads。</summary>
    private static readonly string[] WarnOnlyNames =
    [
        "Desktop", "桌面",
        "Documents", "文档",
        "Downloads", "下载",
        "Pictures", "图片",
        "Videos", "视频",
        "Music", "音乐",
        "Favorites",
        "OneDrive",
        "Dropbox",
    ];

    /// <summary>退回一个规整过的绝对路径（去掉尾部斜杠、展开 8.3 短名、解析相对路径）。</summary>
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("路径不能为空。", nameof(path));

        var full = Path.GetFullPath(path.Trim());

        // 去掉尾部目录分隔符，但保留盘符根 "C:\" 的形式。
        while (full.Length > 3 && (full.EndsWith('\\') || full.EndsWith('/')))
            full = full[..^1];

        return full;
    }

    /// <summary>判断路径是否为某个卷的根（"C:\"）。</summary>
    public static bool IsVolumeRoot(string path)
    {
        var full = Normalize(path);
        var root = Path.GetPathRoot(full);
        return root is not null && string.Equals(
            root.TrimEnd('\\'), full.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>取路径所在的卷根。使用 <c>GetVolumePathName</c>，能正确处理挂载点。</summary>
    public static string GetVolumeRoot(string path)
    {
        try
        {
            var buffer = new char[512];
            if (NativeMethods.GetVolumePathName(Normalize(path), buffer, buffer.Length))
            {
                var s = new string(buffer).TrimEnd('\0');
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
        }
        catch (Exception)
        {
            // 落到托管实现
        }

        return Path.GetPathRoot(Normalize(path)) ?? string.Empty;
    }

    /// <summary>两个路径是否位于同一个卷。</summary>
    public static bool IsSameVolume(string a, string b) =>
        string.Equals(GetVolumeRoot(a), GetVolumeRoot(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>child 是否等于 parent 或位于 parent 之下。</summary>
    public static bool IsSameOrUnder(string child, string parent)
    {
        var c = Normalize(child).TrimEnd('\\');
        var p = Normalize(parent).TrimEnd('\\');
        if (string.Equals(c, p, StringComparison.OrdinalIgnoreCase)) return true;
        return c.StartsWith(p + "\\", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>判断一个源目录是否触碰了硬性禁区。</summary>
    public static bool IsHardBlockedSource(string path)
    {
        var full = Normalize(path);

        foreach (var blocked in HardBlockedPaths)
        {
            if (string.Equals(full.TrimEnd('\\'), blocked.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // AppData / Local / Roaming 这些目录本身不能再被搬走。
        foreach (var suffix in DangerousAncestorSuffixes)
        {
            if (full.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // 用户目录本身（C:\Users\xxx）也不能搬。
        var parent = Path.GetDirectoryName(full);
        if (parent is not null &&
            string.Equals(Path.GetFileName(parent), "Users", StringComparison.OrdinalIgnoreCase))
        {
            var grand = Path.GetDirectoryName(parent);
            if (grand is not null && IsSameOrUnder(grand, Path.GetPathRoot(full)!))
                return true;
        }

        return false;
    }

    private static bool IsWarnOnly(string path)
    {
        var name = Path.GetFileName(Normalize(path).TrimEnd('\\'));
        return WarnOnlyNames.Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 对一次迁移请求做完整的前置检查。返回的报告中 <see cref="PreflightReport.CanProceed"/>
    /// 为 false 时，引擎会直接拒绝执行、不做任何修改。
    /// </summary>
    public static PreflightReport Check(MigrationRequest request)
    {
        var checks = new List<PreflightCheck>();

        string source, destination;
        try
        {
            source = Normalize(request.SourcePath);
            destination = Normalize(request.DestinationPath);
        }
        catch (Exception ex)
        {
            checks.Add(new PreflightCheck("路径格式", false, $"路径无法解析：{ex.Message}"));
            return new PreflightReport { Checks = checks };
        }

        // ---------- 源路径 ----------
        bool isLinkOnly = request.Mode == MigrationMode.LinkOnly;

        if (isLinkOnly)
        {
            // 链接模式下 SourcePath 是"链接应该出现的位置"，
            // 它本来就应该还不存在（或者是个空的占位目录），所以不能要求它存在。
            bool notThere = !Directory.Exists(source) && !File.Exists(source);
            bool emptyDir = Directory.Exists(source) && ReparsePoint.IsDirectoryEmpty(source);

            checks.Add(new PreflightCheck("原位置可以放置链接", notThere || emptyDir,
                notThere ? "原位置目前是空的，会把链接建在这里。"
                : emptyDir ? "原位置是一个空文件夹，会先清掉再建链接。"
                : $"原位置已被占用且非空：{source}。出于安全考虑不会覆盖现有内容。"));
        }
        else
        {
            bool sourceExists = Directory.Exists(source) || File.Exists(source);
            checks.Add(new PreflightCheck("源路径存在", sourceExists, sourceExists ? source : $"找不到：{source}"));

            if (!sourceExists) return new PreflightReport { Checks = checks };

            bool sourceIsDir = Directory.Exists(source);
            checks.Add(new PreflightCheck("源是目录", sourceIsDir,
                sourceIsDir ? "OK" : "目前只支持目录搬家，请选择文件夹。"));

            if (!sourceIsDir) return new PreflightReport { Checks = checks };
        }

        checks.Add(new PreflightCheck("源不是盘符根目录", !IsVolumeRoot(source),
            IsVolumeRoot(source) ? "不能把整个盘符搬走。" : "OK"));

        bool hardBlocked = IsHardBlockedSource(source);
        checks.Add(new PreflightCheck("源不是系统关键目录", !hardBlocked,
            hardBlocked ? $"出于安全考虑，禁止搬走：{source}" : "OK"));

        if (IsWarnOnly(source))
        {
            checks.Add(new PreflightCheck("已知文件夹提醒", false,
                $"「{Path.GetFileName(source)}」是 Windows 已知文件夹（桌面/文档/下载等）。" +
                "搬走并建链接通常可行，但如果有第三方程序硬编码了路径或做特殊处理，可能出现异常。建议先关闭正在使用它的程序。",
                IsBlocking: false));
        }

        // 源本身已经是链接了？
        var sourceLinkKind = ReparsePoint.GetLinkKind(source);
        checks.Add(new PreflightCheck("源不是链接", sourceLinkKind == LinkKind.None,
            sourceLinkKind == LinkKind.None
                ? "OK"
                : $"源本身已经是一个{(sourceLinkKind == LinkKind.Junction ? "目录联接" : "符号链接")}，" +
                  $"指向 {ReparsePoint.ReadLinkTarget(source) ?? "?"}。不需要再搬一次。"));

        // 源不能是当前正在运行的应用自身所在目录（否则边跑边搬自己）
        var appDir = AppContext.BaseDirectory;
        checks.Add(new PreflightCheck("源不是正在运行的程序目录", !IsSameOrUnder(appDir, source),
            IsSameOrUnder(appDir, source) ? "不能搬走程序自己所在的目录。" : "OK"));

        // 源不能包含用户配置文件的关键子目录？已由上面的 AppData 检查覆盖。

        // ---------- 目标路径 ----------
        checks.Add(new PreflightCheck("源与目标不同", !string.Equals(source, destination, StringComparison.OrdinalIgnoreCase),
            string.Equals(source, destination, StringComparison.OrdinalIgnoreCase) ? "源和目标是同一个路径。" : "OK"));

        checks.Add(new PreflightCheck("目标不位于源之内", !IsSameOrUnder(destination, source),
            IsSameOrUnder(destination, source) ? "目标在源目录内部，会无限递归。" : "OK"));

        checks.Add(new PreflightCheck("源不位于目标之内", !IsSameOrUnder(source, destination),
            IsSameOrUnder(source, destination) ? "源在目标目录内部，搬运会互相覆盖。" : "OK"));

        if (isLinkOnly)
        {
            // LinkOnly：SourcePath 是链接该出现的位置，DestinationPath 是真实数据所在处。
            // 「原位置能不能放链接」在上面的源路径检查里已经判定过了。
            bool realExists = Directory.Exists(destination);
            checks.Add(new PreflightCheck("真实目录存在", realExists,
                realExists ? destination : $"链接要指向的目录不存在：{destination}"));

            checks.Add(new PreflightCheck("真实目录不是链接", ReparsePoint.GetLinkKind(destination) == LinkKind.None,
                ReparsePoint.GetLinkKind(destination) == LinkKind.None
                    ? "OK"
                    : "要指向的目标本身是另一个链接，请指向它的真实位置。"));
        }
        else
        {
            // MoveAndLink / CopyOnly：目标位置是"数据将要落下的地方"。
            var destinationParent = Path.GetDirectoryName(destination);
            bool parentOk = !string.IsNullOrEmpty(destinationParent) &&
                            (Directory.Exists(destinationParent) || Directory.Exists(GetVolumeRoot(destination)));
            checks.Add(new PreflightCheck("目标位置可写", parentOk,
                parentOk ? destination : $"目标路径的上级目录不存在：{destinationParent}"));

            if (Directory.Exists(destination) && !request.AllowMergeIntoExisting)
            {
                bool empty = ReparsePoint.IsDirectoryEmpty(destination);
                checks.Add(new PreflightCheck("目标未被占用", empty,
                    empty ? "OK" : $"目标已存在且非空：{destination}。请换一个名字，或勾选「允许合并」。"));
            }

            // 已经有硬性拦截项时就没必要去统计源目录体积了 ——
            // 那一步可能要遍历几十万个文件，而在注定被拒绝的情况下纯属浪费用户时间。
            if (checks.Any(c => !c.Passed && c.IsBlocking))
            {
                checks.Add(new PreflightCheck("目标盘剩余空间", false,
                    "前面的检查已经未通过，跳过空间统计。", IsBlocking: false));
            }
            else
            {
                var (spaceOk, spaceDetail) = CheckFreeSpace(source, destination, allowMerge: request.AllowMergeIntoExisting);
                checks.Add(new PreflightCheck("目标盘剩余空间充足", spaceOk, spaceDetail));
            }
        }

        if (request.Mode == MigrationMode.MoveAndLink && request.LinkKind == LinkKind.SymbolicLink)
        {
            var (ok, detail) = LinkService.CheckSymbolicLinkCapability();
            checks.Add(new PreflightCheck("可以创建符号链接", ok, detail));
        }

        return new PreflightReport { Checks = checks };
    }

    /// <summary>估算源目录大小，并和目标盘的剩余空间对比。</summary>
    private static (bool Ok, string Detail) CheckFreeSpace(string source, string destination, bool allowMerge)
    {
        try
        {
            var size = DirectorySizer.Measure(source, CancellationToken.None).SizeBytes;
            var targetRoot = GetVolumeRoot(destination);
            var drive = DriveInfo.GetDrives().FirstOrDefault(d =>
                string.Equals(d.Name.TrimEnd('\\'), targetRoot.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));

            if (drive is null || !drive.IsReady)
                return (true, $"目标盘 {targetRoot} 信息不可读，跳过空间检查。");

            var free = drive.AvailableFreeSpace;
            // 需求 = 源大小 * 1.02 + 512MB 余量
            var needed = (long)(size * 1.02) + 512L * 1024 * 1024;

            if (allowMerge)
                return (true, $"待复制 {DriveInfoModel.FormatBytes(size)}，目标盘剩余 {DriveInfoModel.FormatBytes(free)}（已允许合并，未做强制检查）。");

            if (free < needed)
                return (false,
                    $"空间不足：需要约 {DriveInfoModel.FormatBytes(needed)}，目标盘 {targetRoot} 只剩 {DriveInfoModel.FormatBytes(free)}。");

            return (true, $"需要约 {DriveInfoModel.FormatBytes(needed)}，{targetRoot} 剩余 {DriveInfoModel.FormatBytes(free)}。");
        }
        catch (Exception ex)
        {
            return (true, $"空间检查跳过：{ex.Message}");
        }
    }

    /// <summary>把源目录下"目标位置已有同名文件"的情况提前列出来，方便用户决定是否合并。</summary>
    public static IReadOnlyList<string> PreviewConflicts(string source, string destination, int max = 200)
    {
        var conflicts = new List<string>();
        if (!Directory.Exists(source) || !Directory.Exists(destination)) return conflicts;

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, file);
            if (File.Exists(Path.Combine(destination, rel)))
            {
                conflicts.Add(rel);
                if (conflicts.Count >= max) break;
            }
        }

        return conflicts;
    }
}
